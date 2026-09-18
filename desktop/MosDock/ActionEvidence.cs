using System.Text.Json;

namespace MosDock;

sealed class ActionEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Action { get; set; } = "";
    public string? Query { get; set; }
    public string? Source { get; set; }
    public bool MatchCase { get; set; }
    public bool WholeWord { get; set; }
    public string? Style { get; set; }
    public int Hits { get; set; }
    public string? Name { get; set; }
    public int? Page { get; set; }
    public string? Format { get; set; }
    public string? Skill { get; set; }
    public bool Ok { get; set; } = true;
    public string? Detail { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

static class ActionEvidence
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    static readonly List<ActionEvent> Items = [];
    static readonly object Gate = new();

    public static IReadOnlyList<ActionEvent> Events
    {
        get
        {
            lock (Gate)
            {
                return Items.ToList();
            }
        }
    }

    public static void Begin(string? attemptId)
    {
        lock (Gate)
        {
            Items.Clear();
            try
            {
                var path = StorePath(attemptId);
                if (File.Exists(path))
                {
                    var loaded = JsonSerializer.Deserialize<List<ActionEvent>>(File.ReadAllText(path), JsonOpts);
                    if (loaded is { Count: > 0 })
                    {
                        Items.AddRange(loaded);
                    }
                }
            }
            catch
            {
                // local cache only
            }
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Items.Clear();
        }
    }

    public static ActionEvent Add(ActionEvent item)
    {
        lock (Gate)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                item.Id = Guid.NewGuid().ToString("N");
            }

            Items.Add(item);
            try
            {
                var path = StorePath(ExamSession.AttemptId);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(Items, JsonOpts));
            }
            catch
            {
                // local cache only
            }
        }

        return item;
    }

    public static ActionEvent Add(
        string action,
        string? query = null,
        string? source = null,
        bool matchCase = false,
        bool wholeWord = false,
        string? style = null,
        int hits = 0,
        string? name = null,
        int? page = null,
        string? format = null,
        string? skill = null,
        bool ok = true,
        string? detail = null)
    {
        return Add(new ActionEvent
        {
            Action = action,
            Query = query,
            Source = source,
            MatchCase = matchCase,
            WholeWord = wholeWord,
            Style = style,
            Hits = hits,
            Name = name,
            Page = page,
            Format = format,
            Skill = skill,
            Ok = ok,
            Detail = detail,
        });
    }

    public static string ToJson()
    {
        lock (Gate)
        {
            return JsonSerializer.Serialize(new { events = Items }, JsonOpts);
        }
    }

    public static LocalCriterion Grade(JsonCriterion item, IReadOnlyList<ActionEvent>? evidence)
    {
        var list = evidence ?? Events;
        if (list.Count == 0)
        {
            return new LocalCriterion(item.Id, "unverified", 0, item.Weight, item.Feedback.Unverified ?? "Chưa xác minh thao tác.");
        }

        return Matches(item, list)
            ? new LocalCriterion(item.Id, "pass", item.Weight, item.Weight, item.Feedback.Pass ?? "Đạt.")
            : new LocalCriterion(item.Id, "fail", 0, item.Weight, item.Feedback.Fail ?? "Chưa đúng thao tác.");
    }

    public static bool Matches(JsonCriterion item, IReadOnlyList<ActionEvent> evidence)
    {
        var pred = item.Predicate;
        var sel = item.Selector;
        var kind = (pred.Type ?? sel.Action ?? "").Trim().ToLowerInvariant();
        var query = First(pred.Query, sel.Query, pred.Text);
        var style = First(pred.Style, sel.Style);
        var name = First(pred.Name, sel.Bookmark);
        var page = pred.Page ?? sel.Page;
        var minHits = pred.MinHits ?? pred.Min ?? 1;
        var matchCase = pred.MatchCase || sel.MatchCase;
        var wholeWord = pred.WholeWord || sel.WholeWord;
        var source = sel.Source;

        foreach (var ev in evidence)
        {
            if (!ev.Ok)
            {
                continue;
            }

            var action = (ev.Action ?? "").Trim().ToLowerInvariant();
            if (kind is "search_query" or "find")
            {
                if (action is not ("find" or "search" or "navigation_pane"))
                {
                    continue;
                }

                if (!QuerySame(ev.Query, query, matchCase))
                {
                    continue;
                }

                if (matchCase && !ev.MatchCase)
                {
                    continue;
                }

                if (wholeWord && !ev.WholeWord)
                {
                    continue;
                }

                if (!SourceOk(source, ev.Source, action))
                {
                    continue;
                }

                return true;
            }

            if (kind is "results_tab")
            {
                if (action is "results_tab" or "results")
                {
                    return true;
                }
            }

            if (kind is "search_navigate" or "find_navigate")
            {
                if (action is not ("find_navigate" or "find" or "search"))
                {
                    continue;
                }

                if (!QuerySame(ev.Query, query, false))
                {
                    continue;
                }

                var hits = ev.Hits > 0 ? ev.Hits : 1;
                if (hits >= minHits)
                {
                    return true;
                }
            }

            if (kind is "advanced_find")
            {
                if (action is not ("advanced_find" or "find"))
                {
                    continue;
                }

                if (!QuerySame(ev.Query, query, false))
                {
                    continue;
                }

                if (!StyleSame(ev.Style, style))
                {
                    continue;
                }

                return true;
            }

            if (kind is "goto_graphic")
            {
                if (action is "goto_graphic" || (action is "goto" && StyleSame(ev.Detail, "graphic")))
                {
                    return true;
                }
            }

            if (kind is "goto_page")
            {
                if (action is not ("goto_page" or "goto"))
                {
                    continue;
                }

                if (page is null || ev.Page == page)
                {
                    return true;
                }
            }

            if (kind is "goto_bookmark")
            {
                if (action is not ("goto_bookmark" or "goto"))
                {
                    continue;
                }

                if (QuerySame(ev.Name ?? ev.Query, name, false))
                {
                    return true;
                }
            }

            if (kind is "save_alternate_format")
            {
                if (action is "save_alternate_format" or "save_as" or "export_pdf")
                {
                    return true;
                }
            }

            if (kind is "print_settings")
            {
                if (action is "print_settings" or "print")
                {
                    return true;
                }
            }

            if (kind is "share_electronic")
            {
                if (action is "share_electronic" or "share")
                {
                    return true;
                }
            }

            if (kind is "inspect_document")
            {
                if (action is "inspect_document" or "inspect")
                {
                    return true;
                }
            }

            if (kind is "compatibility_check")
            {
                if (action is "compatibility_check" or "compatibility")
                {
                    return true;
                }
            }
        }

        return false;
    }

    static bool QuerySame(string? got, string? expected, bool matchCase)
    {
        got = (got ?? "").Trim();
        expected = (expected ?? "").Trim();
        if (expected.Length == 0)
        {
            return true;
        }

        return matchCase
            ? string.Equals(got, expected, StringComparison.Ordinal)
            : string.Equals(got, expected, StringComparison.OrdinalIgnoreCase);
    }

    static bool StyleSame(string? got, string? expected)
    {
        expected = Key(expected);
        if (expected.Length == 0)
        {
            return true;
        }

        return string.Equals(Key(got), expected, StringComparison.Ordinal);
    }

    static bool SourceOk(string? required, string? got, string action)
    {
        var want = Key(required);
        if (want.Length == 0)
        {
            return true;
        }

        var have = Key(got);
        if (want is "navigationpane" or "nav" or "pane")
        {
            return have is "navigationpane" or "nav" or "pane" or "find" || action is "find" or "navigation_pane";
        }

        return have == want || have.Contains(want, StringComparison.Ordinal);
    }

    static string Key(string? value) =>
        string.Concat((value ?? "").Where(c => !char.IsWhiteSpace(c) && c is not '_' and not '-')).ToLowerInvariant();

    static string? First(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    static string StorePath(string? attemptId)
    {
        if (string.IsNullOrWhiteSpace(attemptId))
        {
            return Path.Combine(ExamSession.DataDir, "demo-actions.json");
        }

        return Path.Combine(ExamSession.DataDir, "attempts", attemptId, "actions.json");
    }
}
