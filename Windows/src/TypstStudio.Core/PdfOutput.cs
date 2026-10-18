namespace TypstStudio.Core;

/// Publish complete PDF bytes beside the entry, retaining the previous file on failure.
public sealed class PdfOutput
{
    private readonly SemaphoreSlim _gate = new(1);
    public Task WriteAsync(string entry, byte[] bytes, CancellationToken token = default) => Task.Run(async () =>
    {
        await _gate.WaitAsync(token);
        var target = Path.ChangeExtension(Path.GetFullPath(entry), ".pdf");
        var temporary = Path.Combine(Path.GetDirectoryName(target)!, ".studio-save-pdf-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            if (File.Exists(target) || Directory.Exists(target) || new FileInfo(target).LinkTarget != null)
            {
                var attributes = File.GetAttributes(target);
                if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.ReadOnly)) != 0)
                    throw new IOException("The output PDF is not a writable regular file.");
                if ((await File.ReadAllBytesAsync(target, token)).AsSpan().SequenceEqual(bytes)) return;
            }
            await File.WriteAllBytesAsync(temporary, bytes, token);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, target, true);
        }
        finally { try { File.Delete(temporary); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { } _gate.Release(); }
    }, token);
}
