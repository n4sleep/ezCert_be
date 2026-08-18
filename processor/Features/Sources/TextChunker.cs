namespace EzCert.Processor.Features.Sources;

// Shared markdown -> chunks pipeline (seed content and crawled sources use the
// same normalization so retrieval behavior is consistent).
public static class TextChunker
{
    public static IEnumerable<(string Section, string Text)> Chunk(string markdown, int maxLen, int overlap)
    {
        // split on headings first
        var sections = System.Text.RegularExpressions.Regex.Split(markdown, @"(?m)^(#{1,3} .*)$")
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var current = "";
        var heading = "";
        foreach (var part in sections)
        {
            if (part.StartsWith('#'))
            {
                heading = part.Trim('#').Trim();
                continue;
            }
            var clean = Clean(part);
            if (string.IsNullOrWhiteSpace(clean)) continue;
            if (current.Length + clean.Length + 2 <= maxLen)
            {
                current = current.Length == 0 ? clean : current + "\n\n" + clean;
            }
            else
            {
                if (current.Length > 0) yield return (heading, current);
                current = clean;
            }
        }
        if (current.Length > 0) yield return (heading, current);
    }

    public static string Clean(string text)
    {
        text = text.Replace("�?", "'").Replace("�?", "'").Replace("�?", "-").Replace("�?", "\"");
        // strip inline code fences and URLs noise
        text = System.Text.RegularExpressions.Regex.Replace(text, @"!\[[^\]]*\]\([^)]*\)", "");
        return System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"\s+", " ");
    }

    public static string? ExtractCanonicalUrl(string markdown)
    {
        var m = System.Text.RegularExpressions.Regex.Match(markdown, @"canonicalUrl: (https?://\S+)");
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    public static string? ExtractTitle(string markdown)
    {
        var m = System.Text.RegularExpressions.Regex.Match(markdown, @"(?m)^title:\s*(.+)$");
        return m.Success ? m.Groups[1].Value.Trim().Trim('"') : null;
    }
}
