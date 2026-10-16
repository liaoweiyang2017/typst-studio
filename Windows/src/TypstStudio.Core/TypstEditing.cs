namespace TypstStudio.Core;

// Styles are UTF-16 spans here; the native editor adapter converts them to UTF-8 bytes.
public readonly record struct SyntaxSpan(int Start, int Length, int Style);
public static class TypstEditing
{
    private static readonly HashSet<string> Keywords = ["let", "set", "show", "import", "include", "if", "else", "for", "while", "in", "return", "break", "continue", "true", "false", "none", "auto", "context", "as"];
    public static List<SyntaxSpan> Highlight(string text)
    {
        var spans = new List<SyntaxSpan>(); var i = 0; var math = false;
        while (i < text.Length)
        {
            var start = i; var c = text[i]; var style = 0;
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/') { i += 2; while (i < text.Length && text[i] != '\n') i++; style = 1; }
            else if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i += 2; var depth = 1;
                while (i < text.Length && depth > 0) { if (i + 1 < text.Length && text[i..(i + 2)] == "/*") { depth++; i += 2; } else if (i + 1 < text.Length && text[i..(i + 2)] == "*/") { depth--; i += 2; } else i++; } style = 1;
            }
            else if (c == '"') { i++; while (i < text.Length) { if (text[i] == '\\') { i = Math.Min(i + 2, text.Length); continue; } if (text[i++] == '"') break; } style = 2; }
            else if (c == '`') { var count = 0; while (i < text.Length && text[i] == '`') { count++; i++; } var end = text.IndexOf(new string('`', count), i, StringComparison.Ordinal); i = end < 0 ? text.Length : end + count; style = 2; }
            else if (c == '\\') { i = Math.Min(i + 2, text.Length); }
            else if (c == '$') { math = !math; i++; style = 5; }
            else if (c == '#' || char.IsLetter(c) || c == '_')
            { if (c == '#') i++; while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] is '_' or '-')) i++; if (i == start) i++; var word = text[(c == '#' ? start + 1 : start)..i]; style = Keywords.Contains(word) ? 3 : c == '#' || (i < text.Length && text[i] == '(') ? 4 : math ? 5 : 0; }
            else if (char.IsDigit(c)) { i++; while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.')) i++; style = 6; }
            else if (!math && c == '=' && (i == 0 || text[i - 1] == '\n')) { while (i < text.Length && text[i] != '\n') i++; style = 7; }
            else { i++; style = math ? 5 : 0; }
            if (style != 0) spans.Add(new(start, i - start, style));
        }
        return spans;
    }
    public static (int Start, int Length, string Text) ToggleComment(string text, int start, int length)
    {
        var from = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        var last = Math.Min(text.Length, start + length);
        if (length > 0 && last > 0 && text[last - 1] == '\n') last--;
        var to = text.IndexOf('\n', last); if (to < 0) to = text.Length;
        var lines = text[from..to].Split('\n'); var nonempty = lines.Where(l => l.Trim().Length > 0).ToArray(); var remove = nonempty.Length > 0 && nonempty.All(l => l.TrimStart().StartsWith("//"));
        for (var i = 0; i < lines.Length; i++) { var indent = lines[i].Length - lines[i].TrimStart().Length; if (remove) { if (lines[i][indent..].StartsWith("//")) { var count = lines[i][(indent + 2)..].StartsWith(' ') ? 3 : 2; lines[i] = lines[i].Remove(indent, count); } } else lines[i] = lines[i].Insert(indent, "// "); }
        return (from, to - from, string.Join('\n', lines));
    }
}
