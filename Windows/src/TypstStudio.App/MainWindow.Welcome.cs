using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using static TypstStudio.App.UiText;
namespace TypstStudio.App;
public sealed partial class MainWindow
{
    [Microsoft.UI.Xaml.Data.Bindable]
    public sealed record RecentRow(string Name, string Path, string Prefix, string Tail);
    private void RefreshRecent()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        RecentProjects.ItemsSource = _recent.Select(path =>
        {
            var name = new DirectoryInfo(path).Name;
            var display = path.StartsWith(home + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? "~" + path[home.Length..] : path;
            return new RecentRow(name, path, display.EndsWith(name, StringComparison.OrdinalIgnoreCase) ? display[..^name.Length] : display, display.EndsWith(name, StringComparison.OrdinalIgnoreCase) ? name : "");
        }).ToArray();
        RecentEmpty.Visibility = _recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Ui.Describe(WelcomeFolderButton, T("打开文件夹 (Ctrl+O)", "Open Folder (Ctrl+O)"));
        Ui.Describe(WelcomeFileButton, T("打开文件 (Ctrl+Shift+O)", "Open File (Ctrl+Shift+O)"));
        Ui.Describe(WelcomeTemplateButton, T("从模板新建 (Ctrl+N)", "New from Template (Ctrl+N)"));
    }
    private async void RecentProjects_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_opening || e.ClickedItem is not RecentRow row) return;
        SetStatus(T("正在打开 ", "Opening ") + row.Name + "…"); await Task.Yield(); await SafeAsync(() => OpenProjectAsync(row.Path));
    }
    private void RecentRow_PointerEntered(object sender, PointerRoutedEventArgs e) { if ((sender as FrameworkElement)?.FindName("RecentMore") is Button button) button.Opacity = 1; }
    private void RecentRow_PointerExited(object sender, PointerRoutedEventArgs e) { if ((sender as FrameworkElement)?.FindName("RecentMore") is Button button && button.FocusState == FocusState.Unfocused) button.Opacity = 0; }
    private void RecentRow_GotFocus(object sender, RoutedEventArgs e) { if ((sender as FrameworkElement)?.FindName("RecentMore") is Button button) button.Opacity = 1; }
    private void RecentMore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path } button) return;
        var menu = new MenuFlyout();
        var reveal = new MenuFlyoutItem { Text = T("在资源管理器中打开", "Show in File Explorer"), Icon = new SymbolIcon(Symbol.Folder) };
        reveal.Click += async (_, _) => await SafeAsync(async () => { if (!Directory.Exists(path)) throw new IOException(T("工程文件夹不存在。", "The project folder no longer exists.")); await Windows.System.Launcher.LaunchFolderPathAsync(path); });
        var remove = new MenuFlyoutItem { Text = T("从列表移除", "Remove from recents"), Icon = new SymbolIcon(Symbol.Remove) };
        remove.Click += (_, _) => { _recent.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase)); SaveSettings(); RefreshRecent(); };
        menu.Items.Add(reveal); menu.Items.Add(remove); menu.ShowAt(button);
    }
}
