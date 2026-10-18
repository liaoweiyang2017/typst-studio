using System.Reflection;
using System.Text;
using NetSparkleUpdater;
using NetSparkleUpdater.AssemblyAccessors;
using NetSparkleUpdater.Configurations;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.Interfaces;
using NetSparkleUpdater.SignatureVerifiers;

namespace TypstStudio.Core;
public enum UpdateStage { Idle, Checking, Current, Downloading, Ready, Installing, Canceled, Error }
public sealed record UpdateState(UpdateStage Stage, string Version = "", int Progress = 0, string Detail = "");

/// NetSparkle owns download verification and installer handoff; the host owns UI and safe closing.
public sealed class UpdateService : IDisposable
{
    private readonly SparkleUpdater _sparkle;
    private readonly FeedDownloader _feeds;
    private readonly Version _current;
    private readonly string _architecture;
    private readonly bool _allowLoopback;
    private AppCastItem? _offer;
    private string? _download;
    private int _operation;
    private volatile bool _acceptDownload;
    private volatile bool _disposed;
    public UpdateState State { get; private set; } = new(UpdateStage.Idle);
    public event Action<UpdateState>? Changed;
    public Func<Task<bool>> PrepareToExit { get; set; } = () => Task.FromResult(false);
    public Func<Task> CloseApplication { get; set; } = () => Task.CompletedTask;
    public event Action? InstallerStarting;

    public UpdateService(string feed, string publicKey, Version current, string architecture, string installDirectory, string cacheDirectory, bool allowLoopback = false)
    {
        _current = current; _architecture = architecture; _allowLoopback = allowLoopback;
        _feeds = new FeedDownloader(allowLoopback);
        _sparkle = new SparkleUpdater(feed, new Ed25519Checker(SecurityMode.Strict, publicKey, publicKeyFile: null, readFileBeingVerifiedInChunks: true), typeof(UpdateService).Assembly.Location)
        {
            UIFactory = null, LogWriter = null, UserInteractionMode = UserInteractionMode.DownloadNoInstall,
            Configuration = new VersionConfiguration(current), AppCastDataDownloader = _feeds,
            RelaunchAfterUpdate = false, CheckServerFileName = false, TmpDownloadFilePath = cacheDirectory,
            CustomInstallerArguments = InstallerArguments(installDirectory, Path.Combine(cacheDirectory, "install.log"))
        };
        _sparkle.DownloadMadeProgress += (_, _, e) => { if (_acceptDownload) Set(new(UpdateStage.Downloading, _offer?.Version ?? "", e.ProgressPercentage)); };
        _sparkle.DownloadFinished += (item, path) => {
            if (!_acceptDownload || _disposed) return;
            if (new FileInfo(path).Length != item.UpdateSize) { Set(new(UpdateStage.Error, Detail: "The downloaded package has an unexpected size.")); return; }
            _download = path; Set(new(UpdateStage.Ready, item.Version ?? ""));
        };
        _sparkle.DownloadCanceled += (_, _) => Set(new(UpdateStage.Canceled));
        _sparkle.DownloadHadError += (_, _, error) => { if (_acceptDownload) Set(new(UpdateStage.Error, Detail: error.Message)); };
        _sparkle.DownloadedFileIsCorrupt += (_, _) => { if (_acceptDownload) Set(new(UpdateStage.Error, Detail: "The update signature is invalid.")); };
        _sparkle.DownloadedFileThrewWhileCheckingSignature += (_, _) => { if (_acceptDownload) Set(new(UpdateStage.Error, Detail: "The update could not be verified.")); };
        _sparkle.PreparingToExitAsync += async (_, e) => {
            try { e.Cancel = !await PrepareToExit(); }
            catch (Exception error) { e.Cancel = true; Set(new(UpdateStage.Error, Detail: error.Message)); }
            if (e.Cancel && State.Stage != UpdateStage.Error) Set(new(UpdateStage.Ready, _offer?.Version ?? ""));
        };
        _sparkle.CloseApplicationAsync += async () => await CloseApplication();
        _sparkle.InstallerProcessAboutToStart += (_, _) => { InstallerStarting?.Invoke(); return true; };
        _sparkle.InstallUpdateFailed += (reason, _) => { Set(new(UpdateStage.Error, Detail: "Installer handoff failed: " + reason)); return false; };
    }
    private void Set(UpdateState state) { if (_disposed) return; State = state; Changed?.Invoke(state); }
    public Task CheckAsync() => Task.Run(async () => {
        if (_disposed || State.Stage is UpdateStage.Downloading or UpdateStage.Ready or UpdateStage.Installing || Interlocked.CompareExchange(ref _operation, 1, 0) != 0) return;
        try {
            Set(new(UpdateStage.Checking));
            var result = await _sparkle.CheckForUpdatesQuietly(true);
            if (_disposed) return;
            if (result.Status == UpdateStatus.CouldNotDetermine) throw new IOException("Unable to download or verify the update feed.");
            if (result.Status != UpdateStatus.UpdateAvailable) { Set(new(UpdateStage.Current)); return; }
            var offer = result.Updates.FirstOrDefault(i => IsCompatible(i, _current, _architecture, _allowLoopback));
            if (offer == null) throw new IOException("No verified package matches this Windows architecture.");
            _offer = offer; _download = null; _acceptDownload = true;
            _sparkle.TmpDownloadFileNameWithExtension = Path.GetFileName(new Uri(offer.DownloadLink!).AbsolutePath);
            Set(new(UpdateStage.Downloading, offer.Version ?? ""));
            await _sparkle.InitAndBeginDownload(offer);
        } catch (Exception error) { Set(new(UpdateStage.Error, Detail: error.Message)); }
        finally { Interlocked.Exchange(ref _operation, 0); }
    });
    public void CancelDownload() { _acceptDownload = false; _sparkle.CancelFileDownload(); Set(new(UpdateStage.Canceled)); }
    public Task InstallAsync() => Task.Run(async () => {
        if (_disposed || State.Stage != UpdateStage.Ready || _offer == null || _download == null || Interlocked.CompareExchange(ref _operation, 1, 0) != 0) return;
        try { Set(new(UpdateStage.Installing, _offer.Version ?? "")); await _sparkle.InstallUpdate(_offer, _download); }
        catch (Exception error) { Set(new(UpdateStage.Error, Detail: error.Message)); }
        finally { Interlocked.Exchange(ref _operation, 0); }
    });
    public static bool IsCompatible(AppCastItem item, Version current, string architecture, bool allowLoopback = false)
    {
        if (!Version.TryParse(item.Version, out var version) || version <= current || architecture is not ("arm64" or "x64") || item.OperatingSystem != "windows" || item.UpdateSize <= 0 || item.UpdateSize > 1024L * 1024 * 1024 || string.IsNullOrWhiteSpace(item.DownloadSignature)) return false;
        if (!Uri.TryCreate(item.DownloadLink, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return false;
        var file = $"Typst-Studio-{version.ToString(3)}-Windows-{architecture}-Setup.exe";
        if (allowLoopback && uri.IsLoopback && uri.Scheme == "http") return Path.GetFileName(uri.AbsolutePath) == file;
        return uri.Scheme == "https" && uri.Host == "github.com" && uri.AbsolutePath == $"/liaoweiyang2017/typst-studio/releases/download/v{version.ToString(3)}/{file}";
    }
    public static string InstallerArguments(string directory, string log)
    {
        // NetSparkle's Windows handoff uses a batch file with delayed expansion.
        if ((directory + log).IndexOfAny(['"', '%', '!', '\r', '\n']) >= 0) throw new IOException("The installation path contains unsupported command characters.");
        return $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOCLOSEAPPLICATIONS /NORESTARTAPPLICATIONS /STUDIOUPDATE=1 /DIR=\"{Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)}\" /LOG=\"{log}\"";
    }
    public void Dispose() { _disposed = true; _acceptDownload = false; _sparkle.Dispose(); _feeds.Dispose(); }
    private sealed class VersionConfiguration : DefaultConfiguration
    {
        public VersionConfiguration(Version version) : base(new AssemblyDiagnosticsAccessor(typeof(UpdateService).Assembly.Location)) { InstalledVersion = version.ToString(); ApplicationName = "Typst Studio"; CheckForUpdate = true; }
    }
    private sealed class FeedDownloader(bool allowLoopback) : IAppCastDataDownloader, IDisposable
    {
        private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(15) };
        public Encoding GetAppCastEncoding() => Encoding.UTF8;
        public string DownloadAndGetAppCastData(string url) => DownloadAndGetAppCastDataAsync(url).GetAwaiter().GetResult();
        public async Task<string> DownloadAndGetAppCastDataAsync(string url)
        {
            var uri = new Uri(url);
            if (uri.Scheme != "https" && !(allowLoopback && uri.IsLoopback && uri.Scheme == "http")) throw new IOException("Update feeds require HTTPS.");
            using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 1024 * 1024) throw new IOException("Update feed is too large.");
            using var input = await response.Content.ReadAsStreamAsync(); using var output = new MemoryStream();
            var buffer = new byte[8192]; using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            int count; while ((count = await input.ReadAsync(buffer, timeout.Token)) > 0) { if (output.Length + count > 1024 * 1024) throw new IOException("Update feed is too large."); output.Write(buffer, 0, count); }
            return Encoding.UTF8.GetString(output.ToArray());
        }
        public void Dispose() => _client.Dispose();
    }
}
