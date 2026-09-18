namespace MosDock;

static class WordCom
{
    public const int WdStory = 6;
    public const int WdFindStop = 0;
    public const int WdFindContinue = 1;
    public const int WdGoToBookmark = -1;
    public const int WdGoToPage = 1;
    public const int WdGoToGraphic = 8;
    public const int WdGoToAbsolute = 1;
    public const int WdGoToFirst = 1;
    public const int WdGoToLast = 5;
    public const int WdExportFormatPdf = 17;

    public static bool TryBind(out dynamic? word, out dynamic? doc)
    {
        word = null;
        doc = null;
        try
        {
            var com = OfficeCapture.TryGet("Word.Application");
            if (com is null)
            {
                return false;
            }

            word = com;
            doc = BindDocument(word);
            return doc is not null;
        }
        catch
        {
            word = null;
            doc = null;
            return false;
        }
    }

    public static bool WaitForWord(int timeoutMs = 15000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < until)
        {
            if (TryBind(out _, out _))
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return TryBind(out _, out _);
    }

    static dynamic? BindDocument(dynamic word)
    {
        dynamic docs = word.Documents;
        int count = (int)docs.Count;
        var wanted = ExamSession.LocalPath;
        if (!string.IsNullOrWhiteSpace(wanted))
        {
            for (int i = 1; i <= count; i++)
            {
                dynamic item = docs[i];
                string full = (string)item.FullName;
                if (OfficeCapture.SamePath(full, wanted))
                {
                    item.Activate();
                    return item;
                }
            }
        }

        try
        {
            return word.ActiveDocument;
        }
        catch
        {
            return count >= 1 ? docs[1] : null;
        }
    }
}
