using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace TypstStudio.Core;
public record PdfPoint(int Page, double X, double Y);
public record SourcePoint(string Path, int Line, int Column);
public sealed class TinymistSession : IAsyncDisposable
{
    private readonly LspConnection _lsp;
    private readonly SemaphoreSlim _gate = new(1);
    private readonly Dictionary<string, string> _documents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _versions = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _lifetime = new();
    private ClientWebSocket? _socket;
    private TaskCompletionSource<bool>? _frame;
    private TaskCompletionSource<PdfPoint[]>? _forward;
    private TaskCompletionSource<SourcePoint>? _inverse;
    public string Root { get; }
    public string Entry { get; }
    public event Action<string>? DiagnosticChanged;
    public event Action<string>? Failed;
    private readonly ConcurrentDictionary<string, CompilerProblem[]> _diagnostics = new();
    public IReadOnlyList<CompilerProblem> Problems => _diagnostics.Values.SelectMany(p => p).OrderBy(p => p.Path).ThenBy(p => p.Line).ToArray();
    public TinymistSession(string executable, string root, string entry)
    {
        Root = Path.GetFullPath(root); Entry = Path.GetFullPath(entry);
        _lsp = new(executable, Root); _lsp.Notification += OnNotification; _lsp.Exited += message => Failed?.Invoke(message);
    }
    public async Task InitializeAsync(CancellationToken token = default)
    {
        await _lsp.RequestAsync("initialize", new { processId = Environment.ProcessId, rootUri = new Uri(Root + Path.DirectorySeparatorChar).AbsoluteUri,
            capabilities = new { general = new { positionEncodings = new[] { "utf-16" } } },
            initializationOptions = new { rootPath = Root, exportPdf = "never", compileStatus = "enable", customizedShowDocument = true, preview = new { background = new { enabled = false } } } }, token);
        await _lsp.NotifyAsync("initialized", new { }, token);
        await _lsp.CommandAsync("tinymist.pinMain", [Entry], token);
    }
    private async Task UpdateAsync(string path, string text, CancellationToken token)
    {
        if (_documents.TryGetValue(path, out var old) && old == text) return;
        var uri = new Uri(Path.GetFullPath(path)).AbsoluteUri;
        var version = _versions.GetValueOrDefault(path) + 1; _versions[path] = version;
        if (old is null) await _lsp.NotifyAsync("textDocument/didOpen", new { textDocument = new { uri, languageId = "typst", version, text } }, token);
        else await _lsp.NotifyAsync("textDocument/didChange", new { textDocument = new { uri, version }, contentChanges = new[] { new { text } } }, token);
        _documents[path] = text;
    }
    public async Task<byte[]> CompileAsync(IReadOnlyDictionary<string, string> documents, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            foreach (var (path, text) in documents) await UpdateAsync(path, text, token);
            var result = await _lsp.CommandAsync("tinymist.exportPdf", [Entry, new { }, new { write = false }], token);
            return Convert.FromBase64String(result.GetProperty("data").GetString()!);
        }
        finally { _gate.Release(); }
    }
    private async Task EnsurePreviewAsync(CancellationToken token)
    {
        if (_socket?.State == WebSocketState.Open && _frame?.Task.IsCompletedSuccessfully == true) return;
        var result = await _lsp.CommandAsync("tinymist.doStartPreview", [new[] { Entry, $"--root={Root}", "--task-id=studio", "--data-plane-host=127.0.0.1:0", "--control-plane-host=127.0.0.1:0", "--no-open" }], token);
        var port = result.GetProperty("dataPlanePort").GetInt32();
        _socket = new(); _frame = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await _socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), token);
        _ = ReadSocketAsync(_socket);
        await SendSocketAsync("current", token);
        await _frame.Task.WaitAsync(TimeSpan.FromSeconds(20), token);
    }
    public async Task<(byte[] Pdf, PdfPoint[] Points)> ForwardAsync(string path, int line, int column, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            await EnsurePreviewAsync(_lifetime.Token); token.ThrowIfCancellationRequested();
            // Export after preview initialization so the native PDF and mapping have the same world.
            var exported = await _lsp.CommandAsync("tinymist.exportPdf", [Entry, new { }, new { write = false }], token);
            _forward = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await _lsp.CommandAsync("tinymist.scrollPreview", ["studio", new { @event = "panelScrollTo", filepath = path, line, character = column }], _lifetime.Token);
            var points = await _forward.Task.WaitAsync(TimeSpan.FromSeconds(15), _lifetime.Token);
            token.ThrowIfCancellationRequested();
            return (Convert.FromBase64String(exported.GetProperty("data").GetString()!), points);
        }
        finally { _forward = null; _gate.Release(); }
    }
    public async Task<SourcePoint> InverseAsync(int page, double x, double y, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            await EnsurePreviewAsync(_lifetime.Token); token.ThrowIfCancellationRequested();
            _inverse = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await SendSocketAsync("src-point " + JsonSerializer.Serialize(new { page_no = page, x, y }), _lifetime.Token);
            var point = await _inverse.Task.WaitAsync(TimeSpan.FromSeconds(15), _lifetime.Token);
            token.ThrowIfCancellationRequested(); return point;
        }
        finally { _inverse = null; _gate.Release(); }
    }
    private Task SendSocketAsync(string text, CancellationToken token) => _socket!.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)), WebSocketMessageType.Text, true, token);
    private async Task ReadSocketAsync(ClientWebSocket socket)
    {
        try
        {
            var buffer = new byte[64 * 1024];
            while (!_lifetime.IsCancellationRequested)
            {
                using var message = new MemoryStream(); WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _lifetime.Token);
                    if (result.MessageType == WebSocketMessageType.Close) throw new IOException("定位连接已关闭。");
                    message.Write(buffer, 0, result.Count);
                    if (message.Length > 128 * 1024 * 1024) throw new IOException("Preview message exceeds limit.");
                } while (!result.EndOfMessage);
                var bytes = message.ToArray();
                if (bytes.AsSpan().StartsWith("jump,"u8))
                {
                    var points = Encoding.UTF8.GetString(bytes).Split(',').Skip(1).Where(s => s.Length > 0).Select(s =>
                    {
                        var f = s.Split(' '); return new PdfPoint(int.Parse(f[0]), double.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture), double.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture));
                    }).ToArray();
                    _forward?.TrySetResult(points);
                }
                else if (!bytes.AsSpan().StartsWith("invert-colors"u8)) _frame?.TrySetResult(true);
            }
        }
        catch (Exception error) { _frame?.TrySetException(error); _forward?.TrySetException(error); _inverse?.TrySetException(error); }
    }
    private void OnNotification(string method, JsonElement p)
    {
        if (method == "tinymist/preview/scrollSource")
        {
            var path = p.GetProperty("filepath").GetString();
            if (string.IsNullOrEmpty(path) || p.GetProperty("start").ValueKind != JsonValueKind.Array) _inverse?.TrySetException(new InvalidOperationException("此位置没有源码映射。请点击可见文字或公式。"));
            else _inverse?.TrySetResult(new(path, p.GetProperty("start")[0].GetInt32(), p.GetProperty("start")[1].GetInt32()));
        }
        else if (method == "textDocument/publishDiagnostics")
        {
            var uri = p.GetProperty("uri").GetString()!;
            var path = new Uri(uri).IsFile ? new Uri(uri).LocalPath : uri;
            _diagnostics[uri] = p.GetProperty("diagnostics").EnumerateArray().Select(d => {
                var start = d.GetProperty("range").GetProperty("start");
                return new CompilerProblem(path, start.GetProperty("line").GetInt32(), start.GetProperty("character").GetInt32(), d.GetProperty("message").GetString() ?? "", d.TryGetProperty("severity", out var severity) ? severity.GetInt32() : 1);
            }).ToArray();
            DiagnosticChanged?.Invoke(string.Join("\n", Problems.Select(d => $"{d.Path}:{d.Line + 1}: {d.Message}")));
        }
    }
    public async ValueTask DisposeAsync() { _lifetime.Cancel(); _socket?.Abort(); _socket?.Dispose(); await _lsp.DisposeAsync(); _lifetime.Dispose(); }
}
