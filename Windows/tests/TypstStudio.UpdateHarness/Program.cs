// Isolated installation test. Accepts only a dedicated QA directory and installer.
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chaos.NaCl;
using TypstStudio.Core;

if (args.Length == 2 && args[0] == "--verify-release") {
    var root = Path.GetFullPath(args[1]);
    var key = Convert.FromBase64String(File.ReadAllText(Path.Combine(root, "Windows", "UpdatePublicKey.txt")).Trim());
    foreach (var architecture in new[] { "arm64", "x64" }) {
        var path = Path.Combine(root, "dist", $"appcast-windows-{architecture}.xml");
        if (!Ed25519.Verify(Convert.FromBase64String(File.ReadAllText(path + ".signature").Trim()), File.ReadAllBytes(path), key)) throw new Exception("Feed signature failed");
        var enclosure = System.Xml.Linq.XDocument.Load(path).Descendants("enclosure").Single();
        var signature = (string)enclosure.Attribute(System.Xml.Linq.XName.Get("signature", "http://www.andymatuschak.org/xml-namespaces/sparkle"))!;
        var payloadPath = Path.Combine(root, "Windows", "artifacts", "installers", Path.GetFileName(new Uri((string)enclosure.Attribute("url")!).AbsolutePath));
        if (!Ed25519.Verify(Convert.FromBase64String(signature), File.ReadAllBytes(payloadPath), key)) throw new Exception("Package signature failed");
        Console.WriteLine($"PASS: production feed and installer signatures ({architecture})");
    }
    return;
}

if (args.Length != 4) throw new ArgumentException("installer installDirectory architecture report");
var installer = Path.GetFullPath(args[0]); var directory = Path.GetFullPath(args[1]); var arch = args[2]; var report = Path.GetFullPath(args[3]);
if (!directory.Contains("QA022", StringComparison.Ordinal) || !installer.Contains("validation-022", StringComparison.Ordinal)) throw new ArgumentException("Only isolated QA packages may be used");
Directory.CreateDirectory(directory);
var payload = await File.ReadAllBytesAsync(installer);
Ed25519.KeyPairFromSeed(out var publicKey, out var privateKey, RandomNumberGenerator.GetBytes(32));
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
var url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";
var name = $"Typst-Studio-0.2.2-Windows-{arch}-Setup.exe";
var feed = Encoding.UTF8.GetBytes($"<?xml version=\"1.0\" encoding=\"utf-8\"?><rss version=\"2.0\" xmlns:sparkle=\"http://www.andymatuschak.org/xml-namespaces/sparkle\"><channel><title>QA</title><item><enclosure url=\"{url}{name}\" sparkle:version=\"0.2.2\" sparkle:os=\"windows\" length=\"{payload.Length}\" sparkle:signature=\"{Convert.ToBase64String(Ed25519.Sign(payload, privateKey))}\" type=\"application/octet-stream\"/></item></channel></rss>");
var files = new Dictionary<string, byte[]> { ["/appcast.xml"] = feed, ["/appcast.xml.signature"] = Encoding.UTF8.GetBytes(Convert.ToBase64String(Ed25519.Sign(feed, privateKey))), ["/" + name] = payload };
_ = Task.Run(async () => {
    while (true) {
        var socket = await listener.AcceptTcpClientAsync();
        _ = Task.Run(async () => { using (socket) {
            try {
                var stream = socket.GetStream(); var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var first = await reader.ReadLineAsync(); string? line; while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync())) { }
                var path = first?.Split(' ').ElementAtOrDefault(1) ?? "";
                if (!files.TryGetValue(path, out var data)) { await stream.WriteAsync("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray()); return; }
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {data.Length}\r\nContent-Type: application/octet-stream\r\nConnection: close\r\n\r\n"));
                await stream.WriteAsync(data);
            } catch (IOException) { }
        }});
    }
});
using var updates = new UpdateService(url + "appcast.xml", Convert.ToBase64String(publicKey), new Version(0, 2, 1), arch, directory, Path.Combine(directory, "update-cache"), allowLoopback: true);
async Task Ready() {
    await updates.CheckAsync();
    for (var i = 0; i < 1200 && updates.State.Stage is UpdateStage.Checking or UpdateStage.Downloading; i++) await Task.Delay(50);
    if (updates.State.Stage != UpdateStage.Ready) throw new IOException("Update not ready: " + updates.State);
}
int installs = 0; updates.InstallerStarting += () => installs++;
await Ready();
updates.PrepareToExit = () => Task.FromResult(false); await updates.InstallAsync();
if (installs != 0 || updates.State.Stage != UpdateStage.Ready) throw new Exception("Canceled save started installation");
var source = Path.Combine(directory, "unsaved.typ"); await File.WriteAllTextAsync(source, "original");
var buffer = new SourceBuffer(source, "original") { Text = "saved before updating" };
await File.WriteAllTextAsync(source, "external edit");
updates.PrepareToExit = async () => { await buffer.SaveAsync(); return true; };
await updates.InstallAsync();
if (installs != 0 || await File.ReadAllTextAsync(source) != "external edit") throw new Exception("Save conflict was ignored");
await Ready(); await File.WriteAllTextAsync(source, "original");
updates.CloseApplication = async () => {
    await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { architecture = arch, cancelProtected = true, conflictProtected = true, sourceSaved = (await File.ReadAllTextAsync(source)) == buffer.Text, installerHandedOff = installs == 1 }));
    Environment.Exit(0);
};
await updates.InstallAsync();
throw new Exception("Updater did not close after approved handoff: " + updates.State);
