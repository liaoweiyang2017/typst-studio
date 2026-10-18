using Microsoft.UI.Xaml;
namespace TypstStudio.App;
public partial class App : Application
{
    private Window? _window;
    // Retained until process exit; Inno refuses to replace this installation while any instance is alive.
    private static readonly Mutex RunningMutex = new(false, @"Local\TypstStudio-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.Unicode.GetBytes(Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant()))).ToLowerInvariant());
    internal static void Trace(string text)
    {
        if (!Environment.GetCommandLineArgs().Contains("--smoke")) return;
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "TypstStudio-startup-" + Environment.ProcessId + ".log"), text + "\n");
    }
    public App()
    {
        Trace("App constructor");
        UnhandledException += (_, e) => { Trace(e.Exception.ToString()); foreach (System.Collections.DictionaryEntry item in e.Exception.Data) Trace(item.Key + "=" + item.Value); };
        try { InitializeComponent(); if (Environment.GetCommandLineArgs().Contains("--smoke")) { DebugSettings.IsXamlResourceReferenceTracingEnabled = true; DebugSettings.XamlResourceReferenceFailed += (_, e) => Trace(e.Message); DebugSettings.BindingFailed += (_, e) => Trace(e.Message); } Trace("App resources loaded"); } catch (Exception e) { Trace(e.ToString()); foreach (System.Collections.DictionaryEntry item in e.Data) Trace(item.Key + "=" + item.Value); throw; }
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try { GC.KeepAlive(RunningMutex); Trace("Creating main window"); _window = new MainWindow(); Trace("Activating"); _window.Activate(); Trace("Activated"); }
        catch (Exception e) { Trace(e.ToString()); foreach (System.Collections.DictionaryEntry item in e.Data) Trace(item.Key + "=" + item.Value); throw; }
    }
}
