using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Chaos.NaCl;
using TypstStudio.Core;

internal static class UpdateTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "TypstStudio-update-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        using var server = new Server();
        Ed25519.KeyPairFromSeed(out var pub, out var key, RandomNumberGenerator.GetBytes(32));
        var keyText = Convert.ToBase64String(pub);
        var payload = Encoding.UTF8.GetBytes("Signed test fixture; never executed.");
        string Feed(string version, string architecture, byte[] bytes) => $"<?xml version=\"1.0\" encoding=\"utf-8\"?><rss version=\"2.0\" xmlns:sparkle=\"http://www.andymatuschak.org/xml-namespaces/sparkle\"><channel><title>Fixture</title><item><title>Update</title><enclosure url=\"{server.Url}Typst-Studio-{version}-Windows-{architecture}-Setup.exe\" sparkle:version=\"{version}\" sparkle:os=\"windows\" length=\"{bytes.Length}\" type=\"application/octet-stream\" sparkle:signature=\"{Convert.ToBase64String(Ed25519.Sign(bytes, key))}\" /></item></channel></rss>";
        void Publish(string version = "9.0.0", string arch = "x64", byte[]? bytes = null) {
            bytes ??= payload; var feed = Encoding.UTF8.GetBytes(Feed(version, arch, bytes));
            server.Files["/appcast.xml"] = feed;
            server.Files["/appcast.xml.signature"] = Encoding.UTF8.GetBytes(Convert.ToBase64String(Ed25519.Sign(feed, key)));
            server.Files[$"/Typst-Studio-{version}-Windows-{arch}-Setup.exe"] = bytes;
        }
        UpdateService New() => new(server.Url + "appcast.xml", keyText, new Version(0, 2, 2), "x64", root, Path.Combine(root, Guid.NewGuid().ToString("N")), allowLoopback: true);
        async Task Wait(UpdateService service) {
            for (var i = 0; i < 400 && service.State.Stage is UpdateStage.Checking or UpdateStage.Downloading; i++) await Task.Delay(25);
            Check(service.State.Stage is not (UpdateStage.Checking or UpdateStage.Downloading), "Update test timed out");
        }
        try {
            Publish(); using (var service = New()) {
                int started = 0; service.InstallerStarting += () => started++;
                await Task.WhenAll(service.CheckAsync(), service.CheckAsync()); await Wait(service);
                Check(service.State.Stage == UpdateStage.Ready, "Signed update not ready: " + service.State.Detail);
                service.PrepareToExit = () => Task.FromResult(false); await service.InstallAsync();
                Check(started == 0 && service.State.Stage == UpdateStage.Ready, "Cancel saving must not start installer");
            }
            Publish(); server.Files["/appcast.xml"] = Encoding.UTF8.GetBytes(Feed("9.0.1", "x64", payload));
            using (var service = New()) { await service.CheckAsync(); await Wait(service); Check(service.State.Stage == UpdateStage.Error, "Tampered feed accepted"); }
            Publish(); server.Files["/Typst-Studio-9.0.0-Windows-x64-Setup.exe"] = "corrupted"u8.ToArray();
            using (var service = New()) { await service.CheckAsync(); await Wait(service); Check(service.State.Stage == UpdateStage.Error, "Tampered installer accepted"); }
            Publish("0.2.1"); using (var service = New()) { await service.CheckAsync(); Check(service.State.Stage == UpdateStage.Current, "Downgrade offered"); }
            Publish("9.0.0", "arm64"); using (var service = New()) { await service.CheckAsync(); Check(service.State.Stage == UpdateStage.Error, "Wrong architecture accepted"); }
            Publish(); server.Files.TryRemove("/appcast.xml", out _);
            using (var service = New()) { await service.CheckAsync(); Check(service.State.Stage == UpdateStage.Error, "Missing feed did not recover"); }
            var large = RandomNumberGenerator.GetBytes(512 * 1024); Publish(bytes: large); server.Slow = true;
            using (var service = New()) { await service.CheckAsync(); service.CancelDownload(); await Task.Delay(300); Check(service.State.Stage == UpdateStage.Canceled, "Cancellation did not settle"); }
            Console.WriteLine("PASS: signed feed/package, canceled install, tamper rejection, downgrade, wrong architecture, missing feed and canceled download");
        } finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
    private sealed class Server : IDisposable
    {
        private readonly HttpListener _listener = new();
        public readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> Files = new();
        public bool Slow;
        public string Url { get; }
        public Server() {
            var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
            Url = $"http://127.0.0.1:{port}/"; _listener.Prefixes.Add(Url); _listener.Start(); _ = Serve();
        }
        private async Task Serve() {
            while (_listener.IsListening) {
                HttpListenerContext c; try { c = await _listener.GetContextAsync(); } catch { return; }
                _ = Task.Run(async () => {
                    try {
                        if (!Files.TryGetValue(c.Request.Url!.AbsolutePath, out var bytes)) { c.Response.StatusCode = 404; return; }
                        c.Response.ContentLength64 = bytes.Length;
                        for (var i = 0; i < bytes.Length; i += 4096) { if (Slow && c.Request.Url.AbsolutePath.EndsWith(".exe")) await Task.Delay(20); await c.Response.OutputStream.WriteAsync(bytes.AsMemory(i, Math.Min(4096, bytes.Length - i))); }
                    } catch (Exception e) when (e is IOException or HttpListenerException or ObjectDisposedException) { }
                    finally { c.Response.Close(); }
                });
            }
        }
        public void Dispose() { _listener.Stop(); _listener.Close(); }
    }
}
