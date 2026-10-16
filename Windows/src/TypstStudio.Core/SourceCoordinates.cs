using System.Text;
namespace TypstStudio.Core;
public static class SourceCoordinates
{
    public static (int Line, int Column) Position(string text, int utf16Offset)
    {
        var end = Math.Clamp(utf16Offset, 0, text.Length);
        var prefix = text[..end];
        var start = prefix.LastIndexOf('\n') + 1;
        return (prefix.Count(c => c == '\n'), prefix[start..].EnumerateRunes().Count());
    }
    public static int Offset(string text, int line, int column)
    {
        if (line < 0 || column < 0) throw new ArgumentOutOfRangeException();
        var start = 0;
        for (var i = 0; i < line; i++) { var end = text.IndexOf('\n', start); if (end < 0) throw new InvalidDataException("源码位置已过期。"); start = end + 1; }
        var rowEnd = text.IndexOf('\n', start); if (rowEnd < 0) rowEnd = text.Length;
        var offset = start; var count = 0;
        foreach (var rune in text[start..rowEnd].EnumerateRunes()) { if (count++ == column) return offset; offset += rune.Utf16SequenceLength; }
        if (count == column) return offset;
        throw new InvalidDataException("源码列已过期。");
    }
}
