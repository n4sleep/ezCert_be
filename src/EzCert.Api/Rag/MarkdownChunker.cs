using System.Text;
using System.Text.RegularExpressions;

namespace EzCert.Api.Rag;

public sealed record RagChunk(string SectionSlug, string SourceUrl, string Content, int Ordinal);

// Splits a crawled MS Learn markdown file into embeddable chunks.
// One chunk per "## " heading, carrying the nearest "Source:" URL, capped at MaxChunkChars.
public static partial class MarkdownChunker
{
    [GeneratedRegex(@"!\[[^\]]*\]\([^)]*\)")] private static partial Regex ImageRx();
    [GeneratedRegex(@"^\s*Source:\s*(\S+)", RegexOptions.IgnoreCase)] private static partial Regex SourceRx();
    [GeneratedRegex(@"\n{3,}")] private static partial Regex BlankRunRx();

    public static IReadOnlyList<RagChunk> Chunk(string slug, string markdown, int maxChars)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');

        // Fallback origin URL from the front-matter "> Source:" line.
        var originUrl = "";
        foreach (var l in lines.Take(8))
        {
            var m = Regex.Match(l, @">\s*Source:\s*(\S+)", RegexOptions.IgnoreCase);
            if (m.Success) { originUrl = m.Groups[1].Value; break; }
        }

        var chunks = new List<RagChunk>();
        var buf = new StringBuilder();
        var heading = "";
        var sourceUrl = originUrl;
        var ordinal = 0;

        void Flush()
        {
            if (buf.Length == 0) return;
            var text = Clean(buf.ToString());
            if (text.Length >= 40) // skip trivially small fragments
            {
                foreach (var piece in SplitToSize(text, maxChars))
                    chunks.Add(new RagChunk(slug, sourceUrl, piece, ordinal++));
            }
            buf.Clear();
        }

        foreach (var raw in lines)
        {
            if (raw.StartsWith("## "))
            {
                Flush();
                heading = raw[3..].Trim();
                sourceUrl = originUrl;
                buf.AppendLine(heading);
                continue;
            }
            var sm = SourceRx().Match(raw);
            if (sm.Success) { sourceUrl = sm.Groups[1].Value; continue; }
            if (raw.StartsWith("# ") || raw.StartsWith(">") || raw.Trim() == "---") continue;
            buf.AppendLine(raw);
        }
        Flush();
        return chunks;
    }

    private static string Clean(string s)
    {
        s = ImageRx().Replace(s, "");
        s = BlankRunRx().Replace(s, "\n\n");
        return s.Trim();
    }

    // Greedy paragraph packing so no chunk exceeds maxChars.
    private static IEnumerable<string> SplitToSize(string text, int maxChars)
    {
        if (text.Length <= maxChars) { yield return text; yield break; }
        var paras = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var p in paras)
        {
            if (sb.Length > 0 && sb.Length + p.Length + 2 > maxChars)
            {
                yield return sb.ToString().Trim();
                sb.Clear();
            }
            if (p.Length > maxChars)
            {
                // Hard-split an oversized paragraph.
                for (var i = 0; i < p.Length; i += maxChars)
                    yield return p.Substring(i, Math.Min(maxChars, p.Length - i)).Trim();
                continue;
            }
            sb.Append(p).Append("\n\n");
        }
        if (sb.Length > 0) yield return sb.ToString().Trim();
    }
}
