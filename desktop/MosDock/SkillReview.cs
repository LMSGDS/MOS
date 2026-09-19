namespace MosDock;

static class SkillReview
{
    static readonly string[] WordUiTerms =
    [
        "Navigation Pane", "Table of Contents", "Track Changes", "Mail Merge",
        "Page Border", "Page Color", "Page Setup", "Search Document",
        "References", "Mailings", "Developer", "Review", "Insert", "Design",
        "Layout", "Home", "File", "View", "Find", "Replace", "Go To",
        "Bookmark", "Hyperlink", "Footnote", "Endnote", "Citation",
        "Bibliography", "Caption", "Header", "Footer", "Watermark",
        "Styles", "Themes", "SmartArt", "Results", "Headings",
        "Ctrl+F", "Ctrl+H", "Ctrl+G", "Ribbon",
    ];

    public static bool HideScores => ExamSession.Mode == "testing";

    public static string Label(string? status)
    {
        if (HideScores || string.IsNullOrWhiteSpace(status))
        {
            return "—";
        }

        return status switch
        {
            "pass" => "✅  Đạt",
            "fail" => "❌  Chưa đạt",
            "unverified" => "⚠  Chưa XN",
            "error" => "❌  Lỗi",
            _ => status,
        };
    }

    public static int ImageIndex(string? status)
    {
        if (HideScores || string.IsNullOrWhiteSpace(status))
        {
            return 0;
        }

        return status switch
        {
            "pass" => 1,
            "fail" => 2,
            "error" => 2,
            "unverified" => 3,
            _ => 0,
        };
    }

    public static Color ColorOf(string? status)
    {
        if (HideScores || string.IsNullOrWhiteSpace(status))
        {
            return Ui.Muted;
        }

        return status switch
        {
            "pass" => Ui.Success,
            "fail" => Ui.Danger,
            "unverified" => Ui.Warning,
            "error" => Ui.Danger,
            _ => Ui.Text,
        };
    }

    public static string Headline(string? status)
    {
        if (HideScores)
        {
            return "Chế độ thi ẩn kết quả đến khi nộp bài.";
        }

        return status switch
        {
            "pass" => "✅  Đã đạt kỹ năng này",
            "fail" => "❌  Chưa đạt — cần sửa thao tác",
            "unverified" => "⚠  Chưa xác minh thao tác",
            "error" => "❌  Không chấm được mục này",
            _ => "Chưa chấm kỹ năng này",
        };
    }

    public static string Analysis(LocalCriterion hit, JsonCriterion? item)
    {
        var blocks = AnalysisBlocks(hit, item);
        return string.Join("\n\n", blocks.Select(b => "**" + b.Title + "**\n" + b.Body));
    }

    public static IReadOnlyList<ReviewBlock> AnalysisBlocks(LocalCriterion hit, JsonCriterion? item)
    {
        if (HideScores)
        {
            return [new ReviewBlock("Kết quả chấm", "Trong chế độ Thi, MOS-KulKul không phân tích Đạt / Chưa đạt cho đến khi nộp bài.", "result")];
        }

        if (string.IsNullOrWhiteSpace(hit.Status))
        {
            return [new ReviewBlock("Kết quả chấm", "Chưa chấm. Bấm **Chấm lại** để MOS-KulKul đọc tệp Word đang mở và phân tích từng đề mục.", "result")];
        }

        var msg = MarkWordUi(FeedbackText(hit, item));
        return hit.Status switch
        {
            "pass" =>
            [
                new ReviewBlock("Kết quả chấm", "Học sinh đã làm đúng yêu cầu này.\n\n" + msg + ScoreLine(hit), "correct"),
                new ReviewBlock("Ma trận kỹ năng", MatrixBody(hit), "correct"),
            ],
            "fail" => FailBlocks(msg, item, hit),
            "unverified" =>
            [
                new ReviewBlock("Kết quả chấm", "MOS-KulKul chưa ghi nhận được thao tác (**Find**, **Go To**, **Navigation Pane**…). Không kết luận học sinh làm sai.\n\n" + msg, "yours"),
            ],
            "error" =>
            [
                new ReviewBlock("Kết quả chấm", "Không đọc được bằng chứng trong tệp:\n\n" + msg, "pitfall"),
            ],
            _ => [new ReviewBlock("Kết quả chấm", msg, "result")],
        };
    }

    public static IReadOnlyList<string> HintSteps(JsonCriterion? item)
    {
        if (item?.HelpSteps is not { Count: > 0 })
        {
            return [];
        }

        return item.HelpSteps.Select(Ui.StripMarks).Where(s => s.Length > 0).ToArray();
    }

    public static LocalCriterion Find(IReadOnlyList<LocalCriterion> rows, string? id, int index)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            var hit = rows.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(hit.Id))
            {
                return hit;
            }
        }

        if (index >= 0 && index < rows.Count)
        {
            return rows[index];
        }

        return default;
    }

    public static string MarkWordUi(string text)
    {
        var raw = text ?? "";
        foreach (var term in WordUiTerms.OrderByDescending(t => t.Length))
        {
            raw = WrapTerm(raw, term);
        }

        return raw;
    }

    /// <summary>
    /// Q-matrix SOP: engine walks q1 locate → q2 tool → q3 configure.
    /// The first failed node is the skill gap shown under Thao tác của bạn / Thao tác đúng.
    /// </summary>
    static ReviewBlock[] FailBlocks(string msg, JsonCriterion? item, LocalCriterion hit)
    {
        var correct = HintSteps(item);
        var broken = hit.QTrace.FirstOrDefault(n => n.Status == "fail");
        var yours = string.IsNullOrWhiteSpace(broken.Detail)
            ? "Bằng chứng trong tệp đang mở chưa đủ / chưa đúng. So sánh với thao tác chuẩn bên dưới."
            : broken.Detail;
        var right = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(broken.SkillType))
        {
            var idx = broken.StepId is "q2" ? 1 : broken.StepId is "q3" ? 2 : 0;
            if (idx < correct.Count)
            {
                right.Append(MarkWordUi(correct[idx]));
            }
        }

        if (right.Length == 0)
        {
            if (correct.Count == 0)
            {
                right.Append(MarkWordUi(item?.Prompt ?? "Làm đúng yêu cầu trên Ribbon của Word."));
            }
            else
            {
                for (var i = 0; i < correct.Count; i++)
                {
                    if (i > 0)
                    {
                        right.AppendLine();
                    }

                    right.Append(i + 1).Append(". ").Append(MarkWordUi(correct[i]));
                }
            }
        }

        var pit = new System.Text.StringBuilder();
        pit.AppendLine("• Vào nhầm tab trên **Ribbon** (ví dụ **Home** thay vì **References** / **View**).");
        pit.AppendLine("• Gõ lệnh tắt nhưng chưa mở đúng ngăn (**Navigation Pane**, **Find**, **Go To**).");
        if (correct.Count > 0)
        {
            pit.Append("• Bỏ bước: ").Append(MarkWordUi(correct[0]));
        }

        var result = (msg.Length == 0 ? "Tệp Word chưa khớp yêu cầu của mục này." : msg) + ScoreLine(hit);
        return
        [
            new ReviewBlock("Kết quả chấm", result, "result"),
            new ReviewBlock("Ma trận kỹ năng", MatrixBody(hit), "result"),
            new ReviewBlock("Thao tác của bạn", yours, "yours"),
            new ReviewBlock("Thao tác đúng", right.ToString(), "correct"),
            new ReviewBlock("Lỗi thường gặp", pit.ToString().TrimEnd(), "pitfall"),
        ];
    }

    static string MatrixBody(LocalCriterion hit)
    {
        if (hit.QTrace.Count == 0)
        {
            return "Chưa có dấu vết Q-matrix. Bấm **Chấm lại** để phân rã định vị → công cụ → tham số.";
        }

        var lines = new System.Text.StringBuilder();
        foreach (var node in hit.QTrace)
        {
            var mark = node.Status switch
            {
                "pass" => "✅",
                "fail" => "❌",
                "unverified" => "⚠",
                _ => "—",
            };
            if (lines.Length > 0)
            {
                lines.AppendLine();
            }

            lines.Append(mark).Append(' ').Append(node.StepId).Append(" · ").Append(node.Label);
            if (!string.IsNullOrWhiteSpace(node.Detail))
            {
                lines.Append(" — ").Append(MarkWordUi(node.Detail));
            }
        }

        return lines.ToString();
    }

    static string FeedbackText(LocalCriterion hit, JsonCriterion? item)
    {
        var msg = (hit.Message ?? "").Trim();
        if (msg.Length == 0 && item?.Feedback is { } fb)
        {
            msg = hit.Status switch
            {
                "pass" => fb.Pass ?? "",
                "fail" => fb.Fail ?? "",
                "unverified" => fb.Unverified ?? "",
                "error" => fb.Error ?? "",
                _ => "",
            };
        }

        if (msg.Length == 0)
        {
            msg = item?.Prompt ?? "Chưa có mô tả.";
        }

        return msg;
    }

    static string WrapTerm(string text, string term)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term))
        {
            return text;
        }

        var result = new System.Text.StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            var at = text.IndexOf(term, i, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                result.Append(text.AsSpan(i));
                break;
            }

            result.Append(text.AsSpan(i, at - i));
            var taken = text.Substring(at, term.Length);
            var before = at > 0 ? text[at - 1] : ' ';
            var after = at + term.Length < text.Length ? text[at + term.Length] : ' ';
            var already = at >= 2 && text[at - 2] == '*' && text[at - 1] == '*';
            if (already || char.IsLetterOrDigit(before) || char.IsLetterOrDigit(after))
            {
                result.Append(taken);
            }
            else
            {
                result.Append("**").Append(taken).Append("**");
            }

            i = at + term.Length;
        }

        return result.ToString();
    }

    static string ScoreLine(LocalCriterion hit)
    {
        if (hit.Possible <= 0)
        {
            return "";
        }

        return $"\n\nĐiểm mục này: {hit.Earned:0}/{hit.Possible:0}.";
    }
}

readonly record struct ReviewBlock(string Title, string Body, string Tone);
