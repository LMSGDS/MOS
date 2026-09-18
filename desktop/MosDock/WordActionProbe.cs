namespace MosDock;

/// <summary>
/// Quan sát Find đang mở trên Word (Navigation pane / Advanced Find) khi học sinh làm bài.
/// Không gọi Find.Execute để tránh giành điều khiển. Go To chấm nhờ demo hoặc bằng chứng gửi lên.
/// </summary>
static class WordActionProbe
{
    static string _lastKey = "";
    static string _lastQuery = "";
    static int _lastSel = int.MinValue;
    static int _navHits;
    static bool _resultsNoted;

    public static void Reset()
    {
        _lastKey = "";
        _lastQuery = "";
        _lastSel = int.MinValue;
        _navHits = 0;
        _resultsNoted = false;
    }

    public static void Poll()
    {
        if (!WordCom.TryBind(out dynamic? word, out _))
        {
            return;
        }

        try
        {
            dynamic find = word!.Selection.Find;
            var text = ((string?)find.Text ?? "").Trim();
            if (text.Length == 0)
            {
                return;
            }

            var matchCase = ReadBool(find, "MatchCase");
            var wholeWord = ReadBool(find, "MatchWholeWord");
            var style = ReadStyle(find);
            var sel = 0;
            try
            {
                sel = (int)word.Selection.Start;
            }
            catch
            {
                sel = _lastSel;
            }

            var key = $"{text}|{matchCase}|{wholeWord}|{style}";
            var nav = NavPaneVisible(word);
            var source = style.Length > 0 ? "advanced_find" : nav ? "navigation_pane" : "find";

            if (key != _lastKey)
            {
                _lastKey = key;
                _lastQuery = text;
                _lastSel = sel;
                _navHits = 1;
                if (style.Length > 0)
                {
                    ActionEvidence.Add("advanced_find", query: text, source: source, matchCase: matchCase, wholeWord: wholeWord, style: style, hits: 1);
                }
                else
                {
                    ActionEvidence.Add("find", query: text, source: source, matchCase: matchCase, wholeWord: wholeWord, hits: 1);
                }

                if ((nav || source == "navigation_pane") && !_resultsNoted)
                {
                    _resultsNoted = true;
                    ActionEvidence.Add("results_tab", query: text, source: "navigation_pane");
                }

                return;
            }

            if (!string.Equals(text, _lastQuery, StringComparison.OrdinalIgnoreCase) || sel == _lastSel)
            {
                return;
            }

            _lastSel = sel;
            _navHits++;
            if (_navHits >= 2)
            {
                ActionEvidence.Add("find_navigate", query: text, source: source, hits: _navHits);
            }
        }
        catch
        {
            // Word busy
        }
    }

    static bool ReadBool(dynamic find, string name)
    {
        try
        {
            return name == "MatchCase" ? (bool)find.MatchCase : (bool)find.MatchWholeWord;
        }
        catch
        {
            return false;
        }
    }

    static string ReadStyle(dynamic find)
    {
        try
        {
            var style = find.Style;
            if (style is string name)
            {
                return name.Trim();
            }

            try
            {
                return ((string?)style.NameLocal ?? "").Trim();
            }
            catch
            {
                return (style?.ToString() ?? "").Trim();
            }
        }
        catch
        {
            return "";
        }
    }

    static bool NavPaneVisible(dynamic word)
    {
        foreach (var name in new[] { "Navigation", "Navigation Pane", "Task Pane" })
        {
            try
            {
                if ((bool)word.CommandBars[name].Visible)
                {
                    return true;
                }
            }
            catch
            {
                // missing bar
            }
        }

        return false;
    }
}
