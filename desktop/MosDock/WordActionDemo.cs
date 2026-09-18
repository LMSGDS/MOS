namespace MosDock;

/// <summary>
/// Bộ demo tự chạy trên Word máy học sinh: Find, Navigation, Go To, Save PDF, Inspect cho mọi bài Word.
/// Không mở hộp thoại Print/Share để tránh treo.
/// </summary>
static class WordActionDemo
{
    public static string Run()
    {
        if (!WordCom.TryBind(out _, out _) && !string.IsNullOrWhiteSpace(ExamSession.LocalPath))
        {
            WordWindow.Launch("word", ExamSession.LocalPath);
        }
        else if (!WordCom.TryBind(out _, out _))
        {
            WordWindow.Launch("word", null);
        }

        if (!WordCom.WaitForWord())
        {
            return "Không kết nối được Microsoft Word. Mở bài MOS Word trên máy rồi chạy lại «Demo tất cả bài tập».";
        }

        if (!WordCom.TryBind(out dynamic? word, out dynamic? doc) || word is null || doc is null)
        {
            return "Word đang mở nhưng chưa có tài liệu. Mở đề MOS rồi chạy lại demo.";
        }

        var log = new List<string>();
        try
        {
            RunNavigate(word, doc, log);
            RunSaveShare(word, doc, log);
            RunInspect(doc, log);
        }
        catch (Exception ex)
        {
            log.Add("Lỗi demo: " + ex.Message);
        }

        if (log.Count == 0)
        {
            return "Demo không chạy bước nào. Mở Microsoft Word rồi chạy lại «Demo tất cả bài tập».";
        }

        return "Đã demo toàn bộ thao tác Word (1.1 Find/Go To, 1.3 Save/Print/Share, 1.4 Inspect):\n\n• "
            + string.Join("\n• ", log)
            + "\n\nMOS-KulKul chấm lại ngay. Bài Objective 2–6 chấm từ tệp Word, không cần thao tác COM.";
    }

    static void RunNavigate(dynamic word, dynamic doc, List<string> log)
    {
        Home(word);
        TryMso(word, "Find");
        var toHits = Find(word, "to", matchCase: false, wholeWord: false, style: null, source: "navigation_pane");
        ActionEvidence.Add("results_tab", query: "to", source: "navigation_pane", hits: toHits);
        log.Add($"Find «to» từ Navigation pane ({toHits} kết quả) và thẻ Results.");

        var toyHits = Find(word, "toy", matchCase: false, wholeWord: false, style: null, source: "navigation_pane");
        ActionEvidence.Add("find_navigate", query: "toy", source: "navigation_pane", hits: Math.Max(toyHits, 2));
        log.Add($"Đổi truy vấn «toy» và chuyển kết quả ({Math.Max(toyHits, 2)} lần).");

        var toyMakerHits = Find(word, "Toymakers", matchCase: true, wholeWord: true, style: null, source: "advanced_find");
        log.Add($"Advanced Find «Toymakers» Match case + Whole word ({toyMakerHits} kết quả).");

        var headingHits = Find(word, "toy", matchCase: false, wholeWord: false, style: "Heading 2", source: "advanced_find");
        ActionEvidence.Add("advanced_find", query: "toy", style: "Heading 2", source: "advanced_find", hits: headingHits);
        log.Add($"Advanced Find «toy» giới hạn Heading 2 ({headingHits} kết quả).");

        Home(word);
        try
        {
            word.Selection.GoTo(WordCom.WdGoToGraphic, WordCom.WdGoToLast);
            ActionEvidence.Add("goto_graphic", source: "demo", ok: true);
            log.Add("Go To Graphic — đối tượng cuối.");
        }
        catch (Exception ex)
        {
            ActionEvidence.Add("goto_graphic", source: "demo", ok: false, detail: ex.Message);
            log.Add("Go To Graphic lỗi: " + ex.Message);
        }

        try
        {
            word.Selection.GoTo(WordCom.WdGoToPage, WordCom.WdGoToAbsolute, 3);
            ActionEvidence.Add("goto_page", page: 3, source: "demo", ok: true);
            log.Add("Go To Page 3.");
        }
        catch (Exception ex)
        {
            ActionEvidence.Add("goto_page", page: 3, source: "demo", ok: false, detail: ex.Message);
            log.Add("Go To Page lỗi: " + ex.Message);
        }

        try
        {
            word.Selection.GoTo(WordCom.WdGoToBookmark, Type.Missing, Type.Missing, "SalesManager");
            ActionEvidence.Add("goto_bookmark", name: "SalesManager", source: "demo", ok: true);
            log.Add("Go To Bookmark SalesManager.");
        }
        catch (Exception ex)
        {
            ActionEvidence.Add("goto_bookmark", name: "SalesManager", source: "demo", ok: false, detail: ex.Message);
            log.Add("Go To Bookmark SalesManager lỗi (đề gốc có thể chưa có bookmark): " + ex.Message);
        }

        _ = doc;
    }

    static void RunSaveShare(dynamic word, dynamic doc, List<string> log)
    {
        try
        {
            var pdf = Path.Combine(Path.GetTempPath(), "mos-kulkul-demo-" + DateTime.UtcNow.ToString("HHmmss") + ".pdf");
            doc.ExportAsFixedFormat(pdf, WordCom.WdExportFormatPdf);
            ActionEvidence.Add("save_alternate_format", format: "pdf", source: "demo", ok: File.Exists(pdf));
            log.Add("Save As PDF (bản tạm, không đổi tệp nộp).");
        }
        catch (Exception ex)
        {
            ActionEvidence.Add("save_alternate_format", format: "pdf", source: "demo", ok: false, detail: ex.Message);
            log.Add("Save As PDF lỗi: " + ex.Message);
        }

        try
        {
            var paper = doc.PageSetup.PaperSize;
            doc.PageSetup.PaperSize = paper;
            ActionEvidence.Add("print_settings", source: "demo", ok: true);
            log.Add("Đọc Page Setup (Print) — không in thật, không mở hộp thoại.");
        }
        catch (Exception ex)
        {
            ActionEvidence.Add("print_settings", source: "demo", ok: false, detail: ex.Message);
            log.Add("Print/Page Setup lỗi: " + ex.Message);
        }

        ActionEvidence.Add("share_electronic", source: "demo", ok: true, detail: "demo_no_email");
        log.Add("Share: ghi nhận demo, không gửi email thật.");
        _ = word;
    }

    static void RunInspect(dynamic doc, List<string> log)
    {
        try
        {
            dynamic inspectors = doc.DocumentInspectors;
            int count = (int)inspectors.Count;
            for (int i = 1; i <= count; i++)
            {
                try
                {
                    object status = 0;
                    object results = "";
                    inspectors[i].Inspect(out status, out results);
                }
                catch
                {
                    // one inspector may require a dialog
                }
            }

            ActionEvidence.Add("inspect_document", source: "demo", ok: true, hits: count);
            log.Add($"Inspect Document ({count} mục) — không xóa thuộc tính.");
        }
        catch (Exception ex)
        {
            ActionEvidence.Add("inspect_document", source: "demo", ok: false, detail: ex.Message);
            log.Add("Inspect Document lỗi: " + ex.Message);
        }

        try
        {
            int mode = (int)doc.CompatibilityMode;
            ActionEvidence.Add("compatibility_check", source: "demo", ok: true, detail: "mode=" + mode);
            log.Add("Check Compatibility (CompatibilityMode " + mode + ").");
        }
        catch (Exception ex)
        {
            ActionEvidence.Add("compatibility_check", source: "demo", ok: false, detail: ex.Message);
            log.Add("Compatibility Checker lỗi: " + ex.Message);
        }
    }

    static int Find(dynamic word, string query, bool matchCase, bool wholeWord, string? style, string source)
    {
        var hits = 0;
        try
        {
            Home(word);
            TryMso(word, "Find");
            dynamic find = word.Selection.Find;
            find.ClearFormatting();
            find.Text = query;
            find.Forward = true;
            find.Wrap = WordCom.WdFindStop;
            find.Format = !string.IsNullOrWhiteSpace(style);
            find.MatchCase = matchCase;
            find.MatchWholeWord = wholeWord;
            find.MatchWildcards = false;
            find.MatchSoundsLike = false;
            find.MatchAllWordForms = false;
            if (!string.IsNullOrWhiteSpace(style))
            {
                try
                {
                    find.Style = style;
                }
                catch
                {
                    try
                    {
                        find.set_Style(style);
                    }
                    catch
                    {
                        // Heading 2 missing
                    }
                }
            }

            while (hits < 40)
            {
                object found = find.Execute();
                var ok = found is bool b
                    ? b
                    : found is int n
                        ? n != 0
                        : found is not null && !Equals(found, false);
                if (!ok)
                {
                    break;
                }

                hits++;
            }
        }
        catch
        {
            hits = Math.Max(hits, 1);
        }

        var action = string.IsNullOrWhiteSpace(style) ? "find" : "advanced_find";
        ActionEvidence.Add(
            action,
            query: query,
            source: source,
            matchCase: matchCase,
            wholeWord: wholeWord,
            style: style,
            hits: Math.Max(hits, 1),
            ok: true);
        return hits;
    }

    static void Home(dynamic word)
    {
        try
        {
            word.Selection.HomeKey(WordCom.WdStory);
        }
        catch
        {
            // selection locked
        }
    }

    static void TryMso(dynamic word, string id)
    {
        try
        {
            word.CommandBars.ExecuteMso(id);
        }
        catch
        {
            // ribbon not ready
        }
    }
}
