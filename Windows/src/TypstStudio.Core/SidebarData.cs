namespace TypstStudio.Core;
public record SearchHit(string Path, int Line, int Offset, int Length, string Preview);
public record CompilerProblem(string Path, int Line, int Column, string Message, int Severity);
public record PdfHeading(string Title, PdfPoint? Destination, IReadOnlyList<PdfHeading> Children);
public static class ProjectSearch
{
    public static async Task<IReadOnlyList<SearchHit>> FindAsync(IEnumerable<string> files, IReadOnlyDictionary<string, string> buffers, string query, bool matchCase, CancellationToken token)
    {
        var hits = new List<SearchHit>(); if (string.IsNullOrWhiteSpace(query)) return hits;
        foreach (var path in files)
        {
            token.ThrowIfCancellationRequested(); string text;
            if (!buffers.TryGetValue(path, out text!))
            {
                try { if (new FileInfo(path).Length > 1_000_000) continue; text = SourceBuffer.Normalize(await File.ReadAllTextAsync(path, token)); }
                catch (IOException) { continue; } catch (UnauthorizedAccessException) { continue; }
            }
            var offset = 0; var line = 0;
            foreach (var content in text.Split('\n'))
            {
                token.ThrowIfCancellationRequested(); var index = content.IndexOf(query, matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                if (index >= 0) { hits.Add(new(path, line, offset + index, query.Length, content.Trim().Length > 180 ? content.Trim()[..180] + "…" : content.Trim())); if (hits.Count == 200) return hits; }
                offset += content.Length + 1; line++;
            }
        }
        return hits;
    }
    public static int LineOffset(string text, int line, int column)
    {
        var start = 0;
        for (var i = 0; i < line; i++) { var next = text.IndexOf('\n', start); if (next < 0) return text.Length; start = next + 1; }
        var end = text.IndexOf('\n', start); if (end < 0) end = text.Length;
        return start + Math.Clamp(column, 0, end - start);
    }
}
