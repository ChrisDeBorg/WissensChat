using System.Text;

namespace WissensChat.Core;

public static class Chunker
{
    public static IEnumerable<string> Split(string text, int maxChars = 1200, int overlap = 200)
    {
        var paragraphs = text.Replace("\r", "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(p => HardSplit(p, maxChars));

        var sb = new StringBuilder();
        foreach (var p in paragraphs)
        {
            if (sb.Length > 0 && sb.Length + p.Length + 1 > maxChars)
            {
                var chunk = sb.ToString();
                yield return chunk;
                sb.Clear().Append(Tail(chunk, overlap));
            }
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(p);
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    private static IEnumerable<string> HardSplit(string p, int max)
    {
        while (p.Length > max)
        {
            var cut = p.LastIndexOf(' ', max - 1);
            if (cut < max / 2) cut = max;
            yield return p[..cut].Trim();
            p = p[cut..].Trim();
        }
        if (p.Length > 0) yield return p;
    }

    private static string Tail(string s, int n)
    {
        if (s.Length <= n) return s;
        var t = s[^n..];
        var i = t.IndexOf(' ');
        return i >= 0 ? t[(i + 1)..] : t;
    }
}
