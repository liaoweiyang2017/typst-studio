using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TypstStudio.Core;
using static TypstStudio.App.UiText;
namespace TypstStudio.App;
public sealed partial class MainWindow
{
    private bool _checkUpdatesOnStartup = true, _manualUpdateCheck;
    private UpdateService? _updates;
    private bool _updateExitApproved;
    private TextBlock? _updateSettingsStatus;
    private const string WindowsUpdatePublicKey = "vSNT3Lfejj2T3PbNR+VFkpQV1r6fxaQc5AcqOmKSbXk=";
    private async Task InitializeUpdaterAsync()
    {
        try {
            var architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
            var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TypstStudio", "Windows", "Updates");
            _updates = await Task.Run(() => new UpdateService(
                $"https://github.com/liaoweiyang2017/typst-studio/releases/latest/download/appcast-windows-{architecture}.xml",
                WindowsUpdatePublicKey, typeof(MainWindow).Assembly.GetName().Version!, architecture, AppContext.BaseDirectory, cache));
            _updates.Changed += state => DispatcherQueue.TryEnqueue(() => ApplyUpdateState(state));
            _updates.PrepareToExit = () => OnUIAsync(async () => {
                var current = Environment.ProcessPath!;
                foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(current))) {
                    using (process) {
                        if (process.Id == Environment.ProcessId) continue;
                        try { if (string.Equals(process.MainModule?.FileName, current, StringComparison.OrdinalIgnoreCase)) {
                            SetStatus(T("请先关闭其他 Typst Studio 窗口，再安装更新。", "Close other Typst Studio instances before installing.")); return false;
                        } } catch (System.ComponentModel.Win32Exception) { return false; }
                    }
                }
                var approved = await ConfirmUnsavedAsync();
                if (approved) { _updateExitApproved = true; Editor.IsEnabled = false; Files.IsEnabled = false; MainMenu.IsEnabled = false; Root.IsHitTestVisible = false; }
                return approved;
            });
            _updates.CloseApplication = async () => { await OnUIAsync(async () => { _closing = true; await DisposeProjectAsync(); Close(); return true; }); };
            ApplyUpdateState(_updates.State);
            if (_checkUpdatesOnStartup) await CheckUpdatesAsync(false);
        } catch (Exception error) { ApplyUpdateState(new(UpdateStage.Error, Detail: error.Message)); }
    }
    private Task<T> OnUIAsync<T>(Func<Task<T>> action)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(async () => { try { result.TrySetResult(await action()); } catch (Exception error) { result.TrySetException(error); } })) result.TrySetException(new IOException("Window is closing."));
        return result.Task;
    }
    private string UpdateMessage(UpdateState state) => state.Stage switch {
        UpdateStage.Checking => T("正在后台检查更新…", "Checking for updates in the background…"),
        UpdateStage.Current => T("已经是最新版本。", "You are up to date."),
        UpdateStage.Downloading => T($"正在后台下载 {state.Version} · {state.Progress}%", $"Downloading {state.Version} in the background · {state.Progress}%"),
        UpdateStage.Ready => T($"{state.Version} 已下载并通过验证，可以安装并重启。", $"{state.Version} is downloaded and verified. Ready to install and restart."),
        UpdateStage.Installing => T("正在准备安装，请先处理未保存内容。", "Preparing to install. Save or discard unsaved changes first."),
        UpdateStage.Canceled => T("下载已取消，可以继续编辑。", "Download canceled. You can keep editing."),
        UpdateStage.Error => T("更新失败：", "Update failed: ") + state.Detail,
        _ => T("启动时自动检查，也可手动检查。", "Check automatically at startup or manually.")
    };
    private void ApplyUpdateState(UpdateState state)
    {
        if (state.Stage is UpdateStage.Error or UpdateStage.Ready or UpdateStage.Canceled) {
            _updateExitApproved = false; Editor.IsEnabled = true; Files.IsEnabled = true; MainMenu.IsEnabled = true; Root.IsHitTestVisible = true;
        }
        if (_updateSettingsStatus != null) _updateSettingsStatus.Text = UpdateMessage(state);
        UpdateBar.Title = T("软件更新", "Software update"); UpdateBar.Message = UpdateMessage(state);
        UpdateBar.Severity = state.Stage == UpdateStage.Error ? InfoBarSeverity.Warning : state.Stage is UpdateStage.Current or UpdateStage.Ready ? InfoBarSeverity.Success : InfoBarSeverity.Informational;
        if (_manualUpdateCheck || state.Stage is UpdateStage.Downloading or UpdateStage.Ready or UpdateStage.Installing) UpdateBar.IsOpen = true;
        UpdateAction.Content = state.Stage == UpdateStage.Ready ? T("安装并重启", "Install and restart") : T("重新检查", "Check again");
        UpdateAction.Visibility = state.Stage is UpdateStage.Ready or UpdateStage.Error or UpdateStage.Canceled ? Visibility.Visible : Visibility.Collapsed;
        UpdateAction.IsEnabled = state.Stage != UpdateStage.Installing;
        UpdateProgress.Visibility = state.Stage is UpdateStage.Checking or UpdateStage.Downloading ? Visibility.Visible : Visibility.Collapsed;
        UpdateProgress.IsIndeterminate = state.Stage == UpdateStage.Checking || state.Progress == 0; UpdateProgress.Value = state.Progress;
        UpdateCancel.Content = T("取消下载", "Cancel download"); UpdateCancel.Visibility = state.Stage == UpdateStage.Downloading ? Visibility.Visible : Visibility.Collapsed;
    }
    private async Task CheckUpdatesAsync(bool manual)
    {
        _manualUpdateCheck = manual;
        if (_updates == null) return;
        ApplyUpdateState(_updates.State); await _updates.CheckAsync();
    }
    private async void UpdateAction_Click(object sender, RoutedEventArgs e) { if (_updates?.State.Stage == UpdateStage.Ready) await _updates.InstallAsync(); else await CheckUpdatesAsync(true); }
    private void UpdateCancel_Click(object sender, RoutedEventArgs e) => _updates?.CancelDownload();
}
