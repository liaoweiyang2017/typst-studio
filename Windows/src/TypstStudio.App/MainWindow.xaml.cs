using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using TypstStudio.Core;
using Windows.Storage.Pickers;
using Windows.System;
using Microsoft.UI.Xaml.Media;

namespace TypstStudio.App;
public sealed partial class MainWindow : Window
{
    private readonly Dictionary<string, SourceBuffer> _buffers = new(StringComparer.OrdinalIgnoreCase);
    private TinymistSession? _session;
    private SourceBuffer? _current;
    private string? _root, _entry;
    private string[] _sources = [];
    private CancellationTokenSource _compile = new();
    private CancellationTokenSource _navigation = new();
    private FileSystemWatcher? _watcher;
    private byte[]? _pdf;
    private int _revision, _pdfRevision = -1;
    private bool _settingText, _closing, _disposed, _opening;
    private int _fileEpoch;
    private string _diagnostics = "";
    private ContentDialog? _activeDialog;
    private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TypstStudio", "Windows", "settings.json");
    private readonly List<string> _recent = [];
    private string _theme = "Default";
    private readonly string[] _args = Environment.GetCommandLineArgs();
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);
    public MainWindow()
    {
        App.Trace("Main XAML loading"); InitializeComponent(); App.Trace("Main XAML loaded");
        var dpi = Math.Max(1, GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0);
        AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(1200 * dpi), (int)(800 * dpi)));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter) { presenter.PreferredMinimumWidth = (int)(1000 * dpi); presenter.PreferredMinimumHeight = (int)(600 * dpi); }
        App.Trace("Window resized"); AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Studio.ico"));
        App.Trace("Icon loaded"); SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        if (_args.Contains("--smoke")) _settingsPath = Path.Combine(Path.GetTempPath(), "TypstStudio-Smoke-" + Environment.ProcessId, "settings.json");
        App.Trace("Backdrop set"); LoadSettings(); RefreshRecent(); App.Trace("Settings loaded");
        Preview.SourceRequested += async (page, x, y) => await InverseAsync(page, x, y);
        Preview.Error += message => SetStatus(message);
        AppWindow.Closing += async (_, e) =>
        {
            if (_closing) return;
            e.Cancel = true;
            if (_activeDialog != null) { _activeDialog.Hide(); return; }
            if (await ConfirmUnsavedAsync()) { Root.IsHitTestVisible = false; Editor.IsEnabled = false; _closing = true; await DisposeProjectAsync(); Close(); }
        };
        Root.Loaded += async (_, _) =>
        {
            var smoke = Array.IndexOf(_args, "--smoke");
            if (smoke >= 0 && smoke + 1 < _args.Length) await RunSmokeAsync(_args[smoke + 1]);
            else if (_args.Skip(1).FirstOrDefault(path => File.Exists(path) && Path.GetExtension(path).Equals(".typ", StringComparison.OrdinalIgnoreCase)) is { } file)
                await SafeAsync(() => OpenProjectAsync(Path.GetDirectoryName(Path.GetFullPath(file))!, Path.GetFullPath(file)));
            else SetStatus("选择文件夹或最近工程，开始编写。");
        };
        AddShortcut(VirtualKey.O, VirtualKeyModifiers.Control, async () => await PickFolderAsync());
        AddShortcut(VirtualKey.S, VirtualKeyModifiers.Control, async () => await SaveCurrentAsync());
        AddShortcut(VirtualKey.B, VirtualKeyModifiers.Control, async () => await CompileNowAsync());
        AddShortcut(VirtualKey.J, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, async () => await ForwardAsync());
        AddShortcut(VirtualKey.J, VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu, () => { if (!Preview.NavigateSelection()) SetStatus("请先在 PDF 中选择文字，或 Ctrl+单击公式。"); return Task.CompletedTask; });
    }
    private void AddShortcut(VirtualKey key, VirtualKeyModifiers modifiers, Func<Task> action)
    { var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers }; accelerator.Invoked += async (_, e) => { e.Handled = true; try { await action(); } catch (Exception error) { SetStatus(error.Message); } }; Root.KeyboardAccelerators.Add(accelerator); }
    private void SetStatus(string message) { Status.Text = message; }
    private void InitializePicker(object picker) => WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
    private async void OpenFolder_Click(object sender, RoutedEventArgs e) => await SafeAsync(PickFolderAsync);
    private async Task PickFolderAsync()
    { var picker = new FolderPicker(); InitializePicker(picker); picker.FileTypeFilter.Add("*"); var folder = await picker.PickSingleFolderAsync(); if (folder != null) await OpenProjectAsync(folder.Path); }
    private async void OpenFile_Click(object sender, RoutedEventArgs e) => await SafeAsync(async () =>
    { var picker = new FileOpenPicker(); InitializePicker(picker); picker.FileTypeFilter.Add(".typ"); var file = await picker.PickSingleFileAsync(); if (file != null) await OpenProjectAsync(Path.GetDirectoryName(file.Path)!, file.Path); });
    private async void New_Click(object sender, RoutedEventArgs e) => await SafeAsync(async () =>
    {
        if (!await ConfirmUnsavedAsync()) return;
        var templates = new ComboBox { ItemsSource = new[] { "文章", "空白文档", "数学笔记" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        if (await ShowDialogAsync(new ContentDialog { XamlRoot = Root.XamlRoot, Title = "从模板新建", Content = templates, PrimaryButtonText = "选择保存位置", CloseButtonText = "取消" }) != ContentDialogResult.Primary) return;
        var picker = new FileSavePicker { SuggestedFileName = "main.typ" }; InitializePicker(picker); picker.FileTypeChoices.Add("Typst 文档", new List<string> { ".typ" });
        var file = await picker.PickSaveFileAsync(); if (file == null) return;
        await File.WriteAllTextAsync(file.Path, templates.SelectedIndex == 1 ? "" : "= 新文档\n\n开始编写内容。\n\n$ integral_0^1 x^2 dif x = 1/3 $\n");
        await OpenProjectAsync(Path.GetDirectoryName(file.Path)!, file.Path, confirmed: true);
    });
    private async Task OpenProjectAsync(string root, string? selected = null, bool confirmed = false)
    {
        if (_opening) return;
        if (!confirmed && !await ConfirmUnsavedAsync()) return;
        ShowPage(Workspace); _opening = true; Editor.IsEnabled = false; Files.IsEnabled = false;
        try {
        await DisposeProjectAsync(); _disposed = false; _buffers.Clear(); _current = null; Editor.IsReadOnly = true; SetEditorText(""); UpdateTitle(); _pdf = null; _pdfRevision = -1;
        _root = Path.GetFullPath(root); _revision++; Busy.IsActive = true; SetStatus("正在读取工程…");
        _sources = await Task.Run(() => ProjectFiles.Sources(_root));
        if (_disposed) return;
        _entry = ProjectFiles.MainFile(_sources) ?? selected;
        BuildTree();
        if (_entry == null) { Busy.IsActive = false; SetStatus("文件夹中没有 .typ 文件，请新建文档。"); return; }
        _session = new(Path.Combine(AppContext.BaseDirectory, "tinymist.exe"), _root, _entry);
        _session.DiagnosticChanged += message => DispatcherQueue.TryEnqueue(() => { _diagnostics = message; ProblemsButton.Content = string.IsNullOrWhiteSpace(message) ? "无问题" : "查看问题"; });
        _session.Failed += message => DispatcherQueue.TryEnqueue(() => SetStatus("编译服务已退出。请重新打开工程。 " + message));
        await _session.InitializeAsync();
        await OpenBufferAsync(selected ?? _entry);
        _recent.RemoveAll(p => p.Equals(_root, StringComparison.OrdinalIgnoreCase)); _recent.Insert(0, _root); if (_recent.Count > 10) _recent.RemoveAt(10); SaveSettings();
        _watcher = new(_root) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
        _watcher.Changed += OnFileChanged; _watcher.Created += OnFileChanged; _watcher.Deleted += OnFileChanged;
        _watcher.Renamed += (_, e) => OnFileChanged(null, e); _watcher.EnableRaisingEvents = true;
        await CompileNowAsync();
        } finally { _opening = false; Editor.IsEnabled = true; Files.IsEnabled = true; }
    }
    private void OnFileChanged(object? sender, FileSystemEventArgs e)
    {
        if (e.Name?.Contains(".studio-save-") == true || e.FullPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return;
        DispatcherQueue.TryEnqueue(async () =>
        {
            if (_disposed || _root == null || !ProjectFiles.Inside(_root, e.FullPath)) return;
            try
            {
                if (_buffers.TryGetValue(e.FullPath, out var buffer) && File.Exists(e.FullPath))
                {
                    var text = await File.ReadAllTextAsync(e.FullPath);
                    if (SourceBuffer.Normalize(text) != buffer.SavedText)
                    {
                        if (buffer.Dirty) { SetStatus("磁盘文件已改变；保留当前未保存内容，保存前请检查外部修改。"); return; }
                        var replacement = new SourceBuffer(e.FullPath, text); _buffers[e.FullPath] = replacement;
                        if (_current == buffer) { _current = replacement; SetEditorText(replacement.Text); }
                    }
                }
                ScheduleCompile(400);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { SetStatus(error.Message); }
        });
    }
    private async Task OpenBufferAsync(string path)
    {
        if (!ProjectFiles.Inside(_root!, path)) throw new IOException("只打开当前工程内的源文件。");
        var epoch = ++_fileEpoch;
        if (!_buffers.TryGetValue(path, out var buffer)) { buffer = new(path, await File.ReadAllTextAsync(path)); if (epoch != _fileEpoch || _disposed) return; _buffers[path] = buffer; }
        _current = buffer; SetEditorText(buffer.Text); Editor.IsReadOnly = false; UpdateTitle(); Editor.Focus(FocusState.Programmatic);
    }
    private void SetEditorText(string text)
    {
        _settingText = true;
        try { Editor.Text = text; Editor.Select(0, 0); Editor.ClearUndoRedoHistory(); }
        finally { _settingText = false; }
    }
    private string EditorText() => SourceBuffer.Normalize(Editor.Text);
    private (int Line, int Column) EditorPosition()
    {
        var prefix = SourceBuffer.Normalize(Editor.Text[..Math.Clamp(Editor.SelectionStart, 0, Editor.Text.Length)]);
        return SourceCoordinates.Position(prefix, prefix.Length);
    }
    private int RawOffset(int normalizedOffset)
    {
        var raw = 0; var normalized = 0; var text = Editor.Text;
        while (raw < text.Length && normalized < normalizedOffset)
        {
            if (text[raw] == '\r' && raw + 1 < text.Length && text[raw + 1] == '\n') raw++;
            raw++; normalized++;
        }
        return raw;
    }
    private void SelectSource(int start, int length)
    {
        var a = RawOffset(start); var b = RawOffset(start + length); Editor.Select(a, b - a);
        Editor.Focus(FocusState.Programmatic); Editor.UpdateLayout();
        if (Editor.Text.Length == 0) return;
        var rectangle = Editor.GetRectFromCharacterIndex(Math.Min(a, Editor.Text.Length - 1), a == Editor.Text.Length);
        static ScrollViewer? FindScroll(DependencyObject parent)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer scroll) return scroll;
                if (FindScroll(child) is { } nested) return nested;
            }
            return null;
        }
        if (FindScroll(Editor) is { } viewer) viewer.ChangeView(null, Math.Max(0, viewer.VerticalOffset + rectangle.Y - viewer.ViewportHeight / 3), null, true);
    }
    private void Editor_TextChanged(object sender, RoutedEventArgs e)
    {
        if (_settingText || _current == null) return;
        var text = EditorText(); if (text == _current.Text) return;
        _current.Text = text; UpdateTitle(); ScheduleCompile();
    }
    private void Editor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_current == null) return; var position = EditorPosition();
        PositionText.Text = $"行 {position.Line + 1}，列 {position.Column + 1}";
    }
    private async void Editor_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_current == null || Editor.IsReadOnly) return;
        var control = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (control && e.Key is VirtualKey.B or VirtualKey.I or VirtualKey.U) { e.Handled = true; if (e.Key == VirtualKey.B) await CompileNowAsync(); return; }
        if (e.Key == VirtualKey.Tab) { Editor.SelectedText = "  "; e.Handled = true; }
    }
    private async void Editor_Paste(object sender, TextControlPasteEventArgs e)
    {
        e.Handled = true; if (Editor.IsReadOnly) return;
        try { var content = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent(); if (content.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text)) Editor.SelectedText = SourceBuffer.Normalize(await content.GetTextAsync()); }
        catch (Exception error) { SetStatus(error.Message); }
    }
    private void UpdateTitle() { EditorTitle.Text = _current == null ? "尚未打开文档" : Path.GetFileName(_current.Path) + (_current.Dirty ? " ●" : ""); Title = EditorTitle.Text + " — Typst Studio"; }
    private async void ScheduleCompile(int delay = 160)
    {
        _revision++; _pdfRevision = -1; _navigation.Cancel();
        _compile.Cancel(); _compile.Dispose(); _compile = new(); var token = _compile.Token;
        try { await Task.Delay(delay, token); await CompileNowAsync(token); }
        catch (OperationCanceledException) { }
    }
    private async Task CompileNowAsync(CancellationToken token = default)
    {
        if (_session == null || _disposed) return;
        var revision = _revision; var session = _session;
        var documents = _buffers.ToDictionary(p => p.Key, p => p.Value.Text, StringComparer.OrdinalIgnoreCase);
        Busy.IsActive = true; SetStatus("正在编译…"); var watch = Stopwatch.StartNew();
        try
        {
            var pdf = await session.CompileAsync(documents, token);
            if (revision != _revision || session != _session) return;
            if (_pdf == null || !pdf.AsSpan().SequenceEqual(_pdf)) await Preview.SetDocumentAsync(pdf);
            if (revision != _revision || session != _session) return;
            _pdf = pdf; _pdfRevision = revision; PdfTitle.Text = "主文档 · " + Path.GetFileName(_entry);
            SetStatus($"编译完成 · {watch.Elapsed.TotalMilliseconds:F0} ms · Ctrl+单击 PDF 可跳转源码");
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (revision == _revision) { _pdfRevision = -1; SetStatus("编译失败，保留上次预览。 " + error.Message); } }
        finally { if (revision == _revision) Busy.IsActive = false; }
    }
    private async Task ForwardAsync()
    {
        if (_current == null || _session == null || _pdfRevision != _revision) { SetStatus("请等待编译成功后再定位。"); return; }
        var version = _revision; var session = _session; var source = _current;
        _navigation.Cancel(); _navigation.Dispose(); _navigation = new(); var token = _navigation.Token;
        try
        {
            SetStatus("正在定位 PDF…"); var position = EditorPosition();
            var result = await session.ForwardAsync(source.Path, position.Line, position.Column, token);
            if (version != _revision || session != _session || source != _current) return;
            if (result.Points.Length == 0) { SetStatus("这段源码没有对应的渲染位置。"); return; }
            if (_pdf == null || !result.Pdf.AsSpan().SequenceEqual(_pdf)) await Preview.SetDocumentAsync(result.Pdf);
            if (version != _revision || session != _session || source != _current) return;
            _pdf = result.Pdf;
            Preview.GoTo(result.Points[0]); SetStatus("已定位到 PDF。");
        }
        catch (OperationCanceledException) { } catch (Exception error) { SetStatus(error.Message); }
    }
    private async Task InverseAsync(int page, double x, double y)
    {
        if (_session == null || _pdfRevision != _revision) { SetStatus("请等待编译成功后再定位。"); return; }
        var version = _revision; var session = _session;
        _navigation.Cancel(); _navigation.Dispose(); _navigation = new(); var token = _navigation.Token;
        try
        {
            SetStatus("正在定位源码…"); var point = await session.InverseAsync(page + 1, x, y, token);
            if (version != _revision || session != _session) return;
            await OpenBufferAsync(point.Path);
            if (version != _revision || !StringComparer.OrdinalIgnoreCase.Equals(_current?.Path, point.Path)) return;
            var offset = SourceCoordinates.Offset(_current!.Text, point.Line, point.Column);
            SelectSource(offset, 0);
            SetStatus("已定位到 " + Path.GetFileName(point.Path) + $"，行 {point.Line + 1}。主 PDF 保持不变。");
        }
        catch (OperationCanceledException) { } catch (Exception error) { SetStatus(error.Message); }
    }
    private async void Forward_Click(object sender, RoutedEventArgs e) => await ForwardAsync();
    private async void Compile_Click(object sender, RoutedEventArgs e) => await CompileNowAsync();
    private async void Save_Click(object sender, RoutedEventArgs e) => await SafeAsync(SaveCurrentAsync);
    private async void SaveCopy_Click(object sender, RoutedEventArgs e) => await SafeAsync(async () =>
    {
        if (_current == null) return;
        var text = _current.Text;
        var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(_current.Path) + "-copy.typ" }; InitializePicker(picker); picker.FileTypeChoices.Add("Typst 文档", new List<string> { ".typ" });
        var file = await picker.PickSaveFileAsync(); if (file == null) return;
        var copy = new SourceBuffer(file.Path, await File.ReadAllTextAsync(file.Path)) { Text = text };
        await copy.SaveAsync(); SetStatus("副本已保存。当前文档的编辑状态保持不变。");
    });
    private async Task SaveCurrentAsync() { if (_current == null) return; await _current.SaveAsync(); UpdateTitle(); SetStatus("已保存 " + Path.GetFileName(_current.Path)); }
    private async void Export_Click(object sender, RoutedEventArgs e) => await SafeAsync(async () =>
    {
        if (_pdf == null || _pdfRevision != _revision) { SetStatus("请先成功编译当前内容，再导出 PDF。"); return; }
        var data = _pdf; var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(_entry) }; InitializePicker(picker); picker.FileTypeChoices.Add("PDF 文档", new List<string> { ".pdf" });
        var file = await picker.PickSaveFileAsync(); if (file != null) { await File.WriteAllBytesAsync(file.Path, data); SetStatus("PDF 已导出。"); }
    });
    private void BuildTree()
    {
        if (Files == null) return; Files.RootNodes.Clear(); if (_root == null) return;
        var folders = new Dictionary<string, TreeViewNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in _sources.Where(p => p.Contains(FileFilter.Text ?? "", StringComparison.OrdinalIgnoreCase)))
        {
            var parts = Path.GetRelativePath(_root, file).Split(Path.DirectorySeparatorChar); var prefix = ""; TreeViewNode? parent = null;
            foreach (var part in parts[..^1])
            {
                prefix = Path.Combine(prefix, part);
                if (!folders.TryGetValue(prefix, out var node)) { node = new() { Content = new FileItem(part, null), IsExpanded = true }; folders[prefix] = node; if (parent == null) Files.RootNodes.Add(node); else parent.Children.Add(node); }
                parent = node;
            }
            var leaf = new TreeViewNode { Content = new FileItem(parts[^1], file) }; if (parent == null) Files.RootNodes.Add(leaf); else parent.Children.Add(leaf);
        }
    }
    private sealed record FileItem(string Name, string? Path) { public override string ToString() => Name; }
    private async void Files_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    { var item = args.InvokedItem is TreeViewNode node ? node.Content as FileItem : args.InvokedItem as FileItem; if (item?.Path != null) await SafeAsync(() => OpenBufferAsync(item.Path)); }
    private void FileFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) { if (_sources != null) BuildTree(); }
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => Preview.Zoom(0.85);
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => Preview.Zoom(1.15);
    private void Fit_Click(object sender, RoutedEventArgs e) => Preview.FitWidth();
    private async void Problems_Click(object sender, RoutedEventArgs e) => await ShowMessageAsync("编译问题", string.IsNullOrWhiteSpace(_diagnostics) ? "没有编译问题。" : _diagnostics);
    private async void Find_Click(object sender, RoutedEventArgs e)
    {
        var query = new TextBox { PlaceholderText = "查找当前源码" };
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "查找", Content = query, PrimaryButtonText = "查找", CloseButtonText = "取消" };
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary || _current == null || query.Text.Length == 0) return;
        var start = Math.Min(SourceBuffer.Normalize(Editor.Text[..Math.Min(Editor.SelectionStart + Editor.SelectionLength, Editor.Text.Length)]).Length, _current.Text.Length); var index = _current.Text.IndexOf(query.Text, start, StringComparison.OrdinalIgnoreCase);
        if (index < 0) index = _current.Text.IndexOf(query.Text, StringComparison.OrdinalIgnoreCase);
        if (index >= 0) { SelectSource(index, query.Text.Length); } else SetStatus("没有找到匹配内容。");
    }
    private void ShowPage(FrameworkElement page)
    { Welcome.Visibility = page == Welcome ? Visibility.Visible : Visibility.Collapsed; Workspace.Visibility = page == Workspace ? Visibility.Visible : Visibility.Collapsed; SettingsPage.Visibility = page == SettingsPage ? Visibility.Visible : Visibility.Collapsed; }
    private void Home_Click(object sender, RoutedEventArgs e) { RefreshRecent(); ShowPage(Welcome); }
    private void Back_Click(object sender, RoutedEventArgs e) => ShowPage(_root == null ? Welcome : Workspace);
    private void RefreshRecent()
    {
        RecentProjects.Children.Clear();
        if (_recent.Count == 0) RecentProjects.Children.Add(new TextBlock { Text = "暂无最近工程", Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], Margin = new Thickness(0, 24, 0, 0) });
        foreach (var path in _recent)
        {
            var text = new StackPanel { Spacing = 4 }; text.Children.Add(new TextBlock { Text = Path.GetFileName(path), FontSize = 18, TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }); text.Children.Add(new TextBlock { Text = path, FontSize = 12, TextWrapping = TextWrapping.Wrap, Opacity = 0.65 });
            var row = new Grid { ColumnSpacing = 16 }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.Children.Add(new SymbolIcon(Symbol.Folder)); Grid.SetColumn(text, 1); row.Children.Add(text);
            var button = new Button { Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(16) };
            button.Click += async (_, _) => { button.IsEnabled = false; SetStatus("正在打开 " + Path.GetFileName(path) + "…"); try { await SafeAsync(() => OpenProjectAsync(path)); } finally { button.IsEnabled = true; } }; RecentProjects.Children.Add(button);
        }
    }
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        SettingsContent.Children.Clear();
        var back = new Button { Content = "← 返回" }; back.Click += Back_Click; SettingsContent.Children.Add(back);
        SettingsContent.Children.Add(new TextBlock { Text = "设置", FontSize = 32, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 16) });
        void Heading(string title) => SettingsContent.Children.Add(new TextBlock { Text = title, FontSize = 20, Margin = new Thickness(0, 20, 0, 8) });
        void Row(Symbol icon, string title, string description, FrameworkElement? control = null)
        {
            var grid = new Grid { Padding = new Thickness(20), ColumnSpacing = 20, MinHeight = 84 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(new SymbolIcon(icon)); var text = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center }; text.Children.Add(new TextBlock { Text = title, FontSize = 16 }); text.Children.Add(new TextBlock { Text = description, Opacity = 0.65, TextWrapping = TextWrapping.Wrap }); Grid.SetColumn(text, 1); grid.Children.Add(text);
            if (control != null) { Grid.SetColumn(control, 2); control.VerticalAlignment = VerticalAlignment.Center; grid.Children.Add(control); }
            SettingsContent.Children.Add(new Border { Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"], CornerRadius = new CornerRadius(6), Child = grid });
        }
        Heading("个性化");
        var theme = new ComboBox { Width = 180, ItemsSource = new[] { "跟随系统", "浅色", "深色" }, SelectedIndex = _theme == "Light" ? 1 : _theme == "Dark" ? 2 : 0 };
        theme.SelectionChanged += (_, _) => { _theme = theme.SelectedIndex == 1 ? "Light" : theme.SelectedIndex == 2 ? "Dark" : "Default"; Root.RequestedTheme = Enum.Parse<ElementTheme>(_theme); SaveSettings(); };
        Row(Symbol.Setting, "应用主题", "选择应用的浅色或深色外观，立即生效。", theme);
        Heading("编辑器");
        var font = new Slider { Width = 180, Minimum = 11, Maximum = 24, StepFrequency = 1, Value = Editor.FontSize }; font.ValueChanged += (_, _) => { Editor.FontSize = font.Value; SaveSettings(); };
        Row(Symbol.Font, "编辑字号", "调整源码文字大小。", font);
        Row(Symbol.Document, "自动编译", "编辑后自动编译；切换源文件时保持工程主文档与 PDF。");
        Heading("PDF 与定位"); Row(Symbol.Find, "双向定位", "源码：Ctrl+Shift+J。PDF：Ctrl+单击公式，或选中文字后按 Ctrl+Alt+J。");
        var fit = new Button { Content = "适合宽度" }; fit.Click += Fit_Click; Row(Symbol.Zoom, "页面缩放", "预览默认铺满可用宽度。", fit);
        Heading("系统与关于"); Row(Symbol.Setting, "操作系统", System.Runtime.InteropServices.RuntimeInformation.OSDescription); Row(Symbol.Library, "架构", System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()); Row(Symbol.Help, "Typst Studio", "Windows 0.1.0 · WinUI 3 · Tinymist / Typst · PDFium");
        ShowPage(SettingsPage);
    }
    private async Task<bool> ConfirmUnsavedAsync()
    {
        var dirty = _buffers.Values.Where(b => b.Dirty).ToArray(); if (dirty.Length == 0) return true;
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "保存未保存的文档？", Content = string.Join("\n", dirty.Select(b => Path.GetFileName(b.Path))), PrimaryButtonText = "全部保存", SecondaryButtonText = "放弃修改", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Primary };
        var result = await ShowDialogAsync(dialog); if (result == ContentDialogResult.None) return false;
        if (result == ContentDialogResult.Primary) { try { foreach (var buffer in dirty) await buffer.SaveAsync(); } catch (Exception error) { SetStatus(error.Message); return false; } }
        return true;
    }
    private Task ShowMessageAsync(string title, string content) => ShowDialogAsync(new ContentDialog { XamlRoot = Root.XamlRoot, Title = title, Content = new TextBox { Text = content, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 420 }, CloseButtonText = "关闭" });
    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        if (_activeDialog != null) return ContentDialogResult.None;
        _activeDialog = dialog;
        try { return await dialog.ShowAsync(); }
        finally { if (_activeDialog == dialog) _activeDialog = null; }
    }
    private async Task SafeAsync(Func<Task> action) { try { await action(); } catch (Exception error) { Busy.IsActive = false; SetStatus(error.Message); } }
    private void LoadSettings()
    {
        try { using var settings = JsonDocument.Parse(File.ReadAllText(_settingsPath)); _theme = settings.RootElement.GetProperty("theme").GetString()!; Root.RequestedTheme = Enum.Parse<ElementTheme>(_theme); Editor.FontSize = Math.Clamp(settings.RootElement.GetProperty("fontSize").GetDouble(), 11, 24); _recent.AddRange(settings.RootElement.GetProperty("recent").EnumerateArray().Select(p => p.GetString()!).Where(Directory.Exists)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or KeyNotFoundException or InvalidOperationException or FormatException) { }
    }
    private void SaveSettings()
    { try { Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!); File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new { theme = _theme, fontSize = Editor.FontSize, recent = _recent })); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { SetStatus(error.Message); } }
    private async Task DisposeProjectAsync()
    { _disposed = true; _revision++; _fileEpoch++; _compile.Cancel(); _navigation.Cancel(); _watcher?.Dispose(); _watcher = null; if (_session != null) await _session.DisposeAsync(); _session = null; Preview.Close(); }
    private async Task WaitForCompileAsync(string? expectedText = null)
    {
        for (var i = 0; i < 300; i++) { if (_pdfRevision == _revision && !Busy.IsActive && (expectedText == null || _current?.Text == expectedText)) return; await Task.Delay(50); }
        throw new TimeoutException("Compilation did not settle: " + Status.Text);
    }
    private async Task CaptureAsync(string path)
    {
            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap(); await bitmap.RenderAsync(Root);
            var pixels = await bitmap.GetPixelsAsync();
            using (var stream = File.OpenWrite(path))
            {
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream.AsRandomAccessStream());
                encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray()); await encoder.FlushAsync();
            }
    }
    private async Task RunSmokeAsync(string root)
    {
        var report = Path.Combine(root, "native-smoke.json");
        try
        {
            await OpenProjectAsync(root, confirmed: true);
            if (_pdf == null || _current == null) throw new InvalidOperationException("Initial compile failed: " + Status.Text);
            var offset = _current.Text.IndexOf("alpha", StringComparison.Ordinal); SelectSource(offset, 5);
            await ForwardAsync(); if (!Status.Text.Contains("已定位")) throw new InvalidOperationException(Status.Text);
            var source = Path.Combine(root, "chapter.typ"); var child = await File.ReadAllTextAsync(source); var pos = SourceCoordinates.Position(SourceBuffer.Normalize(child), SourceBuffer.Normalize(child).IndexOf("alpha", StringComparison.Ordinal));
            var target = await _session!.ForwardAsync(source, pos.Line, pos.Column, CancellationToken.None);
            var point = target.Points.First(); Preview.GoTo(point); await InverseAsync(point.Page - 1, point.X + 0.5, point.Y - 1);
            if (_current?.Path != source) throw new InvalidOperationException("Cross-file inverse navigation failed: " + Status.Text);
            var previous = _current.Text; SelectSource(previous.Length, 0); Editor.SelectedText = "\nNative editing smoke test.\n";
            await WaitForCompileAsync(previous + "\nNative editing smoke test.\n"); if (_pdfRevision != _revision || !_current.Dirty) throw new InvalidOperationException("Live editing compile failed");
            Editor.Undo(); await WaitForCompileAsync(previous);
            if (_current.Text != previous) throw new InvalidOperationException("Native undo did not restore the original source");
            await Preview.WaitForRenderAsync();
            await CaptureAsync(Path.Combine(root, "native-smoke.png"));
            Settings_Click(this, new RoutedEventArgs()); await Task.Delay(150); await CaptureAsync(Path.Combine(root, "settings.png"));
            Home_Click(this, new RoutedEventArgs()); await Task.Delay(150); await CaptureAsync(Path.Combine(root, "welcome.png"));
            Back_Click(this, new RoutedEventArgs());
            await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { success = true, architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), status = Status.Text, pages = point.Page, version = "0.1.0" }));
        }
        catch (Exception error) { await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { success = false, error = error.ToString() })); }
        if (_args.Contains("--smoke-exit")) { _closing = true; await DisposeProjectAsync(); Close(); }
        // Without --smoke-exit, keep the owned test window open for visual inspection.
    }
}
