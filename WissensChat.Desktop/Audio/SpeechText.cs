using System.Text;
using System.Text.RegularExpressions;

namespace WissensChat.Desktop.Audio;

public static partial class SpeechText
{
    [GeneratedRegex(@"\[\d+(?:\s*,\s*\d+)*\]")]
    private static partial Regex Citation();

    /// Entfernt Quellenmarker wie [1] und Markdown-Zeichen, die nicht vorgelesen werden sollen.
    public static string Clean(string s) =>
        Citation().Replace(s, "").Replace("*", "").Replace("#", "").Replace("`", "").Trim();

    /// Entnimmt aus dem Puffer alle vollständigen Sätze (Rest bleibt im Puffer).
    public static List<string> TakeSentences(StringBuilder buffer, int minLength = 25)
    {
        var result = new List<string>();
        var s = buffer.ToString();
        var start = 0;
        for (var i = 0; i < s.Length - 1; i++)
        {
            var end = s[i] is '.' or '!' or '?' or ':' or '\n';
            if (end && char.IsWhiteSpace(s[i + 1]) && i - start + 1 >= minLength)
            {
                var t = Clean(s[start..(i + 1)]);
                if (t.Length > 0) result.Add(t);
                start = i + 1;
            }
        }
        buffer.Remove(0, start);
        return result;
    }
}
