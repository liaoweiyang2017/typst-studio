namespace TypstStudio.Core;
public static class ProjectFiles
{
    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase) { ".git", "build", "dist", "target", "node_modules", ".vs", "bin", "obj" };
    public static string[] Sources(string root)
    {
        var result = new List<string>(); var pending = new Stack<string>(); pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            try
            {
                foreach (var path in Directory.EnumerateDirectories(directory))
                    if (!Excluded.Contains(Path.GetFileName(path)) && !File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) pending.Push(path);
                result.AddRange(Directory.EnumerateFiles(directory, "*.typ"));
            }
            catch (UnauthorizedAccessException) { } catch (IOException) { }
        }
        return result.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static string? MainFile(IEnumerable<string> sources) => sources.OrderBy(s => Path.GetFileName(s).Equals("main.typ", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(s => s.Length).FirstOrDefault();
    public static bool Inside(string root, string path) => Path.GetFullPath(path).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
public sealed class SourceBuffer(string path, string text)
{
    public string Path { get; } = path;
    public static string Normalize(string value) => value.Replace("\r\n", "\n").Replace("\r", "\n");
    private readonly string _newline = text.Contains("\r\n") ? "\r\n" : "\n";
    public string Text { get; set; } = Normalize(text);
    public string SavedText { get; private set; } = Normalize(text);
    public bool Dirty => Text != SavedText;
    public async Task SaveAsync()
    {
        if (File.Exists(Path) && Normalize(await File.ReadAllTextAsync(Path)) != SavedText) throw new IOException("文件已被外部程序修改。请先重新打开或另存为，避免覆盖外部修改。");
        var temporary = Path + ".studio-save-" + Guid.NewGuid().ToString("N");
        try { await File.WriteAllTextAsync(temporary, Text.Replace("\n", _newline), new System.Text.UTF8Encoding(false)); File.Move(temporary, Path, overwrite: true); SavedText = Text; }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
