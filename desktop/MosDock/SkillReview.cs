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
        if (HideScores)
        {
            return "Trong chế độ Thi, MOS-KulKul không phân tích Đạt / Chưa đạt cho đến khi nộp bài.";
        }

        if (string.IsNullOrWhiteSpace(hit.Status))
        {
            return "Chưa chấm. Bấm **Chấm lại** để MOS-KulKul đọc tệp Word đang mở và phân tích từng đề mục.";
        }

        var msg = MarkWordUi(FeedbackText(hit, item));
        return hit.Status switch
        {
            "pass" => "Học sinh đã làm đúng yêu cầu này.\n\n" + msg + ScoreLine(hit),
            "fail" => FailAnalysis(msg, item) + ScoreLine(hit),
            "unverified" => "MOS-KulKul chưa ghi nhận được thao tác (**Find**, **Go To**, **Navigation Pane**…). Không kết luận học sinh làm sai.\n\n" + msg,
            "error" => "Không đọc được bằng chứng trong tệp:\n\n" + msg,
            _ => msg,
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

    static string FailAnalysis(string msg, JsonCriterion? item)
    {
        var correct = HintSteps(item);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("**Kết quả chấm**");
        sb.AppendLine(msg.Length == 0 ? "Tệp Word chưa khớp yêu cầu của mục này." : msg);
        sb.AppendLine();
        sb.AppendLine("**Thao tác của bạn**");
        sb.AppendLine("Bằng chứng trong tệp đang mở chưa đủ / chưa đúng. So sánh với thao tác chuẩn bên dưới.");
        sb.AppendLine();
        sb.AppendLine("**Thao tác đúng**");
        if (correct.Count == 0)
        {
            sb.AppendLine(MarkWordUi(item?.Prompt ?? "Làm đúng yêu cầu trên Ribbon của Word."));
        }
        else
        {
            for (var i = 0; i < correct.Count; i++)
            {
                sb.Append(i + 1).Append(". ").AppendLine(MarkWordUi(correct[i]));
            }
        }

        sb.AppendLine();
        sb.AppendLine("**Lỗi thường gặp**");
        sb.AppendLine("• Vào nhầm tab trên **Ribbon** (ví dụ **Home** thay vì **References** / **View**).");
        sb.AppendLine("• Gõ lệnh tắt nhưng chưa mở đúng ngăn (**Navigation Pane**, **Find**, **Go To**).");
        if (correct.Count > 0)
        {
            sb.Append("• Bỏ bước: ").AppendLine(MarkWordUi(correct[0]));
        }

        return sb.ToString().TrimEnd();
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
