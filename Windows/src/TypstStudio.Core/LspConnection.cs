using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace TypstStudio.Core;

public sealed class LspConnection : IAsyncDisposable
{
    private readonly Process _process;
    private readonly SemaphoreSlim _writer = new(1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly CancellationTokenSource _lifetime = new();
    private int _nextId;
    public event Action<string, JsonElement>? Notification;
    public event Action<string>? Exited;
    public LspConnection(string executable, string root)
    {
        _process = new Process { StartInfo = new(executable) { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        _process.StartInfo.ArgumentList.Add("lsp");
        _process.Start();
        _ = ReadAsync();
        _ = DrainErrorsAsync();
    }
    public Task NotifyAsync(string method, object parameters, CancellationToken token = default) => WriteAsync(new { jsonrpc = "2.0", method, @params = parameters }, token);
    public async Task<JsonElement> RequestAsync(string method, object parameters, CancellationToken token = default)
    {
        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;
        try
        {
            await WriteAsync(new { jsonrpc = "2.0", id, method, @params = parameters }, token);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(60), token);
        }
        finally { _pending.TryRemove(id, out _); }
    }
    public Task<JsonElement> CommandAsync(string command, object[] arguments, CancellationToken token = default) => RequestAsync("workspace/executeCommand", new { command, arguments }, token);
    private async Task WriteAsync(object message, CancellationToken token)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        await _writer.WaitAsync(token);
        try { await _process.StandardInput.BaseStream.WriteAsync(header, _lifetime.Token); await _process.StandardInput.BaseStream.WriteAsync(bytes, _lifetime.Token); await _process.StandardInput.BaseStream.FlushAsync(_lifetime.Token); }
        finally { _writer.Release(); }
    }
    private async Task ReadAsync()
    {
        try
        {
            var stream = _process.StandardOutput.BaseStream;
            var one = new byte[1];
            while (!_lifetime.IsCancellationRequested)
            {
                var header = new StringBuilder();
                while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                {
                    if (await stream.ReadAsync(one, _lifetime.Token) == 0) throw new EndOfStreamException("Tinymist 已退出。");
                    header.Append((char)one[0]);
                    if (header.Length > 8192) throw new InvalidDataException("Invalid LSP header.");
                }
                var line = header.ToString().Split("\r\n").First(s => s.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                var length = int.Parse(line.Split(':')[1]);
                if (length <= 0 || length > 256 * 1024 * 1024) throw new InvalidDataException("Invalid LSP message length.");
                var bytes = new byte[length]; await stream.ReadExactlyAsync(bytes, _lifetime.Token);
                using var document = JsonDocument.Parse(bytes);
                var message = document.RootElement;
                if (message.TryGetProperty("method", out var method))
                {
                    var parameters = message.TryGetProperty("params", out var p) ? p.Clone() : default;
                    Notification?.Invoke(method.GetString()!, parameters);
                    if (message.TryGetProperty("id", out var serverId))
                        await WriteAsync(new { jsonrpc = "2.0", id = serverId.Clone(), result = (object?)null }, _lifetime.Token);
                }
                else if (message.TryGetProperty("id", out var requestId) && requestId.TryGetInt32(out var id) && _pending.TryGetValue(id, out var completion))
                {
                    if (message.TryGetProperty("error", out var error)) completion.TrySetException(new InvalidOperationException(error.GetProperty("message").GetString()));
                    else completion.TrySetResult(message.GetProperty("result").Clone());
                }
            }
        }
        catch (Exception error)
        {
            foreach (var entry in _pending.Values) entry.TrySetException(error);
            if (!_lifetime.IsCancellationRequested) Exited?.Invoke(error.Message);
        }
    }
    private async Task DrainErrorsAsync() { try { while (await _process.StandardError.ReadLineAsync(_lifetime.Token) is not null) { } } catch (OperationCanceledException) { } }
    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        try { _process.StandardInput.Close(); if (!_process.HasExited) _process.Kill(entireProcessTree: true); await _process.WaitForExitAsync(); } catch (InvalidOperationException) { }
        _process.Dispose(); _lifetime.Dispose();
    }
}
