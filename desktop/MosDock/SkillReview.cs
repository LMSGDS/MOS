namespace MosDock;

static class SkillReview
{
    public static bool HideScores => ExamSession.Mode == "testing";

    public static string Label(string? status)
    {
        if (HideScores || string.IsNullOrWhiteSpace(status))
        {
            return "—";
        }

        return status switch
        {
            "pass" => "Đạt",
            "fail" => "Chưa đạt",
            "unverified" => "Chưa XN",
            "error" => "Lỗi",
            _ => status,
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
            "pass" => "Đã đạt kỹ năng này",
            "fail" => "Chưa đạt — cần sửa",
            "unverified" => "Chưa xác minh thao tác",
            "error" => "Không chấm được mục này",
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
            return "Chưa chấm. Bấm Chấm lại (hoặc bóng đèn Kiểm tra nhiệm vụ) để MOS-KulKul đọc tệp Word và phân tích từng đề mục.";
        }

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

        msg = Ui.StripMarks(msg);
        return hit.Status switch
        {
            "pass" => "Học sinh đã làm đúng yêu cầu này.\n\n" + msg + ScoreLine(hit),
            "fail" => "Học sinh chưa đạt mục này. Lỗi / thiếu sót:\n\n" + msg + QAxis(msg) + ScoreLine(hit),
            "unverified" => "MOS-KulKul chưa ghi nhận được thao tác (Find, Go To, Navigation pane…). Không kết luận học sinh làm sai.\n\n" + msg,
            "error" => "Không đọc được bằng chứng trong tệp:\n\n" + msg,
            _ => msg,
        };
    }

    public static string Hint(LocalCriterion hit, JsonCriterion? item)
    {
        if (HideScores || hit.Status is "pass" or "" || item?.HelpSteps is not { Count: > 0 })
        {
            return "";
        }

        var step = Ui.StripMarks(item.HelpSteps[0]);
        return "Gợi ý bước đầu: " + step;
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

    static string QAxis(string msg)
    {
        var text = msg.ToLowerInvariant();
        if (text.Contains("không thấy") || text.Contains("missing") || text.Contains("chưa tìm"))
        {
            return "\n\nQ-Matrix: sai bước Định vị — chưa tìm đúng đối tượng trên trang.";
        }

        if (text.Contains("công cụ") || text.Contains("tab") || text.Contains("ribbon") || text.Contains("tool"))
        {
            return "\n\nQ-Matrix: sai bước Chọn công cụ — vào đúng tab/lệnh rồi hãy chỉnh.";
        }

        return "\n\nQ-Matrix: sai bước Cấu hình — đã vào đúng chỗ nhưng tham số chưa khớp đề.";
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
