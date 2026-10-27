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
    private readonly PdfOutput _pdfOutput = new();
    private int _revision, _pdfRevision = -1;
    private bool _settingText, _closing, _disposed, _opening;
    private int _fileEpoch, _compileOperation;
    private string _diagnostics = "";
    private bool _pdfHintShown;
    private ContentDialog? _activeDialog;
    private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TypstStudio", "Windows", "settings.json");
    private readonly List<string> _recent = [];
    private string _theme = "Default";
    private readonly string[] _args = Environment.GetCommandLineArgs();
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);
    public MainWindow()
    {
        App.Trace("Main XAML loading"); InitializeComponent(); InitializeSidebar(); App.Trace("Main XAML loaded");
        var dpi = Math.Max(1, GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0);
        var work = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min((int)(Ui.D("WindowWidth") * dpi), work.Width - (int)Ui.D("Space48")); var height = Math.Min((int)(Ui.D("WindowHeight") * dpi), work.Height - (int)Ui.D("Space48"));
        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(work.X + (work.Width - width) / 2, work.Y + (work.Height - height) / 2, width, height));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter) { presenter.PreferredMinimumWidth = Math.Min((int)(Ui.D("WindowMinWidth") * dpi), width); presenter.PreferredMinimumHeight = Math.Min((int)(Ui.D("WindowMinHeight") * dpi), height); }
        App.Trace("Window resized"); AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Studio.ico"));
        App.Trace("Icon loaded"); ConfigureChrome();
        if (_args.Contains("--smoke") || _args.Contains("--screenshots")) _settingsPath = Path.Combine(Path.GetTempPath(), "TypstStudio-Smoke-" + Environment.ProcessId, "settings.json");
        App.Trace("Backdrop set"); LoadSettings(); InitializeEditorTools(); InitializePdfToolbar(); RefreshRecent(); Root.ActualThemeChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateCaption); App.Trace("Settings loaded");
        Preview.SourceRequested += async (page, x, y) => await InverseAsync(page, x, y);
        Preview.Error += message => SetStatus(message);
        Preview.PageChanged += (_, _) => UpdatePdfToolbar();
        Busy.RegisterPropertyChangedCallback(Microsoft.UI.Xaml.Controls.ProgressRing.IsActiveProperty, (_, _) => UpdateCompilePresentation());
        Closed += (_, _) => _updates?.Dispose();
        AppWindow.Closing += async (_, e) =>
        {
            if (_closing) return;
            e.Cancel = true;
            if (_activeDialog != null) { _activeDialog.Hide(); return; }
            if (await ConfirmUnsavedAsync()) { Root.IsHitTestVisible = false; Editor.IsEnabled = false; _closing = true; await DisposeProjectAsync(); Close(); }
        };
        Root.Loaded += async (_, _) =>
        {
            ApplyLanguage(); UpdateCaption();
            if (!_args.Contains("--smoke") && !_args.Contains("--screenshots")) _ = InitializeUpdaterAsync();
            var screenshots = Array.IndexOf(_args, "--screenshots");
            if (screenshots >= 0 && _args.Length > screenshots + 2) { await CaptureProductAsync(_args[screenshots + 1], _args[screenshots + 2]); return; }
            var smoke = Array.IndexOf(_args, "--smoke");
            if (smoke >= 0 && smoke + 1 < _args.Length) await RunSmokeAsync(_args[smoke + 1]);
            else if (_args.Skip(1).FirstOrDefault(path => File.Exists(path) && Path.GetExtension(path).Equals(".typ", StringComparison.OrdinalIgnoreCase)) is { } file)
                await SafeAsync(() => OpenProjectAsync(Path.GetDirectoryName(Path.GetFullPath(file))!, Path.GetFullPath(file)));
            else SetStatus(UiText.T("选择文件夹或最近工程，开始编写。", "Choose a folder or recent project to begin."));
        };
        Root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(CommentKeyDown), true);
        AddShortcut(VirtualKey.O, VirtualKeyModifiers.Control, async () => await PickFolderAsync());
        AddShortcut(VirtualKey.O, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, PickFileAsync);
        AddShortcut(VirtualKey.N, VirtualKeyModifiers.Control, CreateFromTemplateAsync);
        AddShortcut(VirtualKey.S, VirtualKeyModifiers.Control, async () => await SaveCurrentAsync());
        AddShortcut(VirtualKey.B, VirtualKeyModifiers.Control, async () => await CompileCurrentAsync());
        AddShortcut(VirtualKey.J, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, async () => await ForwardAsync());
        AddShortcut(VirtualKey.J, VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu, () => { if (!Preview.NavigateSelection()) SetStatus(UiText.T("请先在 PDF 中选择文字，或 Ctrl+单击公式。", "Select PDF text or Ctrl+click a formula first.")); return Task.CompletedTask; });
    }
    private void AddShortcut(VirtualKey key, VirtualKeyModifiers modifiers, Func<Task> action)
    { var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers }; accelerator.Invoked += async (_, e) => { e.Handled = true; if (_updateExitApproved) return; try { await action(); } catch (Exception error) { SetStatus(error.Message); } }; Root.KeyboardAccelerators.Add(accelerator); }
    private void SetStatus(string message) { Status.Text = message; UpdateCompilePresentation(); }
    private void InitializePicker(object picker) => WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
    private async void OpenFolder_Click(object sender, RoutedEventArgs e) => await SafeAsync(PickFolderAsync);
    private async Task PickFolderAsync()
    { var picker = new FolderPicker(); InitializePicker(picker); picker.FileTypeFilter.Add("*"); var folder = await picker.PickSingleFolderAsync(); if (folder != null) await OpenProjectAsync(folder.Path); }
    private async void OpenFile_Click(object sender, RoutedEventArgs e) => await SafeAsync(PickFileAsync);
    private async Task PickFileAsync()
    { var picker = new FileOpenPicker(); InitializePicker(picker); picker.FileTypeFilter.Add(".typ"); var file = await picker.PickSingleFileAsync(); if (file != null) await OpenProjectAsync(Path.GetDirectoryName(file.Path)!, file.Path); }
    private async void New_Click(object sender, RoutedEventArgs e) => await SafeAsync(CreateFromTemplateAsync);
    private async Task CreateFromTemplateAsync()
    {
        if (!await ConfirmUnsavedAsync()) return;
        var templates = new ComboBox { ItemsSource = new[] { UiText.T("文章", "Article"), UiText.T("空白文档", "Blank"), UiText.T("数学笔记", "Math notes") }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        if (await ShowDialogAsync(new ContentDialog { XamlRoot = Root.XamlRoot, Title = "从模板新建", Content = templates, PrimaryButtonText = "选择保存位置", CloseButtonText = "取消" }) != ContentDialogResult.Primary) return;
        var picker = new FileSavePicker { SuggestedFileName = "main.typ" }; InitializePicker(picker); picker.FileTypeChoices.Add(UiText.T("Typst 文档", "Typst document"), new List<string> { ".typ" });
        var file = await picker.PickSaveFileAsync(); if (file == null) return;
        await File.WriteAllTextAsync(file.Path, templates.SelectedIndex == 1 ? "" : "= 新文档\n\n开始编写内容。\n\n$ integral_0^1 x^2 dif x = 1/3 $\n");
        await OpenProjectAsync(Path.GetDirectoryName(file.Path)!, file.Path, confirmed: true);
    }
    private async Task OpenProjectAsync(string root, string? selected = null, bool confirmed = false)
    {
        if (_opening) return;
        if (!confirmed && !await ConfirmUnsavedAsync()) return;
        ShowPage(Workspace); _opening = true; Editor.IsEnabled = false; Files.IsEnabled = false;
        try {
        await DisposeProjectAsync(); SidebarPicker.SelectedIndex = 0; _projectQuery.Text = ""; _searchResults.ItemsSource = null; _outlineTree.RootNodes.Clear(); _disposed = false; _buffers.Clear(); _current = null; Editor.IsReadOnly = true; SetEditorText(""); UpdateTitle(); _pdf = null; _pdfRevision = -1;
        _root = Path.GetFullPath(root); _revision++; Busy.IsActive = true; SetStatus(UiText.T("正在读取工程…", "Reading project\u2026"));
        _sources = await Task.Run(() => ProjectFiles.Sources(_root));
        if (_disposed) return;
        _entry = selected ?? ProjectFiles.MainFile(_sources);
        BuildTree();
        if (_entry == null) { Busy.IsActive = false; SetStatus(UiText.T("文件夹中没有 .typ 文件，请新建文档。", "No .typ files in this folder. Create a document to begin.")); return; }
        _session = new(Path.Combine(AppContext.BaseDirectory, "tinymist.exe"), _root, _entry);
        var observedSession = _session;
        _session.DiagnosticChanged += message => DispatcherQueue.TryEnqueue(() => { if (_session != observedSession) return; _diagnostics = message; if (SidebarPicker.SelectedIndex == 4) _ = RefreshSidebarAsync(); UpdateCompilePresentation(); });
        _session.Failed += message => DispatcherQueue.TryEnqueue(() => SetStatus(UiText.T("编译服务已退出。请重新打开工程。 ", "The compiler stopped. Reopen the project. ") + message));
        await _session.InitializeAsync();
        await OpenBufferAsync(selected ?? _entry);
        _recent.RemoveAll(p => p.Equals(_root, StringComparison.OrdinalIgnoreCase)); _recent.Insert(0, _root); if (_recent.Count > 10) _recent.RemoveAt(10); SaveSettings();
        _watcher = new(_root) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
        _watcher.Changed += OnFileChanged; _watcher.Created += OnFileChanged; _watcher.Deleted += OnFileChanged;
        _watcher.Renamed += (_, e) => OnFileChanged(null, e); _watcher.EnableRaisingEvents = true;
        await CompileNowAsync();
        } finally { _opening = false; Editor.IsEnabled = true; Files.IsEnabled = true; UpdateCompilePresentation(); }
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
                        if (buffer.Dirty) { SetStatus(UiText.T("磁盘文件已改变；保留当前未保存内容，保存前请检查外部修改。", "The file changed on disk. Your unsaved edits are retained; review external changes before saving.")); return; }
                        var replacement = new SourceBuffer(e.FullPath, text); _buffers[e.FullPath] = replacement;
                        if (_current == buffer) { _current = replacement; SetEditorText(replacement.Text); }
                    }
                }
                ScheduleCompile(400);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { SetStatus(error.Message); }
        });
    }
    private async Task OpenBufferAsync(string path, bool fromNavigation = false)
    {
        if (!ProjectFiles.Inside(_root!, path)) throw new IOException(UiText.T("只打开当前工程内的源文件。", "Only source files within the current project can be opened."));
        if (!fromNavigation) _navigation.Cancel();
        var epoch = ++_fileEpoch;
        if (!_buffers.TryGetValue(path, out var buffer)) { buffer = new(path, await File.ReadAllTextAsync(path)); if (epoch != _fileEpoch || _disposed) return; _buffers[path] = buffer; }
        Editor.ActivateEditor(); _current = buffer; SetEditorText(buffer.Text); Editor.IsReadOnly = false; UpdateTitle(); Editor.Focus(FocusState.Programmatic); if (SidebarPicker.SelectedIndex is 2 or 3) _ = RefreshSidebarAsync();
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
    }
    private void Editor_TextChanged(object sender, RoutedEventArgs e)
    {
        if (_settingText || _current == null) return;
        var text = EditorText(); if (text == _current.Text) return;
        _current.Text = text; UpdateTitle(); UpdateEditorPosition(); ScheduleCompile(); if (SidebarPicker.SelectedIndex == 1) _ = RefreshSidebarAsync();
    }
    private void Editor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateEditorPosition();
    }
    private async void Editor_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_current == null || Editor.IsReadOnly) return;
        var control = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (control && e.Key is VirtualKey.B or VirtualKey.I or VirtualKey.U) { e.Handled = true; if (e.Key == VirtualKey.B) await CompileCurrentAsync(); return; }

    }

    private void UpdateTitle() { EditorTitle.Text = _current == null ? UiText.Translate("尚未打开文档") : Path.GetFileName(_current.Path) + (_current.Dirty ? " ●" : ""); Title = EditorTitle.Text + " — Typst Studio"; TitleFileName.Text = EditorTitle.Text; SyncChrome(); UpdateCompilePresentation(); }
    private async void ScheduleCompile(int delay = -1)
    {
        _revision++; _pdfRevision = -1; _navigation.Cancel();
        _compile.Cancel(); _compile.Dispose(); _compile = new(); var token = _compile.Token;
        if (!_autoCompile) return;
        try { await Task.Delay(delay < 0 ? _compileDelay : delay, token); await CompileNowAsync(token); }
        catch (OperationCanceledException) { }
    }
    private async Task CompileCurrentAsync()
    {
        if (_session == null || _current == null || _disposed || _opening) return;
        if (Busy.IsActive && string.Equals(_entry, _current.Path, StringComparison.OrdinalIgnoreCase)) return;
        _compile.Cancel(); _compile.Dispose(); _compile = new(); _navigation.Cancel();
        _entry = _current.Path; UpdateTreeEntry(); _revision++; _pdfRevision = -1; _compileError = "";
        UpdateCompilePresentation();
        await CompileNowAsync(_compile.Token);
    }
    private async Task CompileNowAsync(CancellationToken token = default)
    {
        if (_session == null || _disposed) return;
        var operation = ++_compileOperation; var revision = _revision; var session = _session;
        var documents = _buffers.ToDictionary(p => p.Key, p => p.Value.Text, StringComparer.OrdinalIgnoreCase);
        Busy.IsActive = true; SetStatus(UiText.T("正在编译…", "Compiling\u2026")); var watch = Stopwatch.StartNew();
        try
        {
            var pdf = await session.CompileAsync(documents, token, _entry);
            if (revision != _revision || session != _session) return;
            if (_pdf == null || !pdf.AsSpan().SequenceEqual(_pdf)) await Preview.SetDocumentAsync(pdf, () => revision == _revision && session == _session);
            if (revision != _revision || session != _session) return;
            _compileError = ""; _pdf = pdf; _pdfRevision = revision; if (SidebarPicker.SelectedIndex is 2 or 4) _ = RefreshSidebarAsync();
            try { if (_entry != null) await _pdfOutput.WriteAsync(_entry, pdf, token); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
                if (revision == _revision) SetStatus(UiText.T("编译成功，但无法保存同目录 PDF：", "Compiled, but could not save the PDF: ") + error.Message);
                return;
            }
            if (revision != _revision || session != _session) return;
            SetStatus(UiText.English ? $"Compiled · {watch.Elapsed.TotalMilliseconds:F0} ms" : $"编译完成 · {watch.Elapsed.TotalMilliseconds:F0} ms");
            if (!_pdfHintShown) { SetStatus(Status.Text + UiText.T(" · Ctrl+单击 PDF 可定位源码", " · Ctrl+click PDF to locate source")); _pdfHintShown = true; SaveSettings(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (revision == _revision) { _pdfRevision = -1; _compileError = error.Message; if (SidebarPicker.SelectedIndex == 4) _ = RefreshSidebarAsync(); UpdateCompilePresentation(); SetStatus(UiText.T("编译失败，请查看编译问题；保留上次预览。", "Compilation failed. See Problems; the previous preview is retained.")); } }
        finally { if (operation == _compileOperation) { Busy.IsActive = false; if (SidebarPicker.SelectedIndex == 4) _ = RefreshSidebarAsync(); } }
    }
    private async Task ForwardAsync()
    {
        if (_current == null || _session == null || _pdfRevision != _revision) { SetStatus(UiText.T("请等待编译成功后再定位。", "Wait for a successful compile before navigating.")); return; }
        var version = _revision; var session = _session; var source = _current;
        _navigation.Cancel(); _navigation.Dispose(); _navigation = new(); var token = _navigation.Token;
        try
        {
            SetStatus(UiText.T("正在定位 PDF…", "Locating in PDF\u2026")); var position = EditorPosition();
            var result = await session.ForwardAsync(source.Path, position.Line, position.Column, token);
            if (version != _revision || session != _session || source != _current) return;
            if (result.Points.Length == 0) { SetStatus(UiText.T("这段源码没有对应的渲染位置。", "This source has no rendered position.")); return; }
            if (_pdf == null || !result.Pdf.AsSpan().SequenceEqual(_pdf)) await Preview.SetDocumentAsync(result.Pdf, () => version == _revision && session == _session && source == _current);
            if (version != _revision || session != _session || source != _current) return;
            _pdf = result.Pdf;
            Preview.GoTo(result.Points[0]); SetStatus(UiText.T("已定位到 PDF。", "Located in PDF."));
        }
        catch (OperationCanceledException) { } catch (Exception error) { SetStatus(error.Message); }
    }
    private async Task InverseAsync(int page, double x, double y)
    {
        if (_session == null || _pdfRevision != _revision) { SetStatus(UiText.T("请等待编译成功后再定位。", "Wait for a successful compile before navigating.")); return; }
        var version = _revision; var session = _session;
        _navigation.Cancel(); _navigation.Dispose(); _navigation = new(); var token = _navigation.Token;
        try
        {
            SetStatus(UiText.T("正在定位源码…", "Locating source\u2026")); var point = await session.InverseAsync(page + 1, x, y, token);
            if (version != _revision || session != _session) return;
            await OpenBufferAsync(point.Path, fromNavigation: true);
            if (version != _revision || !StringComparer.OrdinalIgnoreCase.Equals(_current?.Path, point.Path)) return;
            var offset = SourceCoordinates.Offset(_current!.Text, point.Line, point.Column);
            SelectSource(offset, 0); Editor.FlashNavigation();
            SetStatus(UiText.English ? $"Located in {Path.GetFileName(point.Path)}, line {point.Line + 1}. Main PDF retained." : "已定位到 " + Path.GetFileName(point.Path) + $"，行 {point.Line + 1}。主 PDF 保持不变。");
        }
        catch (OperationCanceledException) { } catch (Exception error) { SetStatus(error.Message); }
    }
    private async void Forward_Click(object sender, RoutedEventArgs e) => await ForwardAsync();
    private async void Compile_Click(object sender, RoutedEventArgs e) => await CompileCurrentAsync();
    private async void Save_Click(object sender, RoutedEventArgs e) => await SafeAsync(SaveCurrentAsync);
    private async void SaveCopy_Click(object sender, RoutedEventArgs e) => await SafeAsync(async () =>
    {
        if (_current == null) return;
        var text = _current.Text;
        var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(_current.Path) + "-copy.typ" }; InitializePicker(picker); picker.FileTypeChoices.Add(UiText.T("Typst 文档", "Typst document"), new List<string> { ".typ" });
        var file = await picker.PickSaveFileAsync(); if (file == null) return;
        var copy = new SourceBuffer(file.Path, await File.ReadAllTextAsync(file.Path)) { Text = text };
        await copy.SaveAsync(); SetStatus(UiText.T("副本已保存。当前文档的编辑状态保持不变。", "Copy saved. Current document edits are retained."));
    });
    private async Task SaveCurrentAsync() { if (_current == null) return; await _current.SaveAsync(); UpdateTitle(); SetStatus(UiText.T("已保存 ", "Saved ") + Path.GetFileName(_current.Path)); }
    private async void Export_Click(object sender, RoutedEventArgs e) => await SafeAsync(async () =>
    {
        if (_pdf == null || _pdfRevision != _revision) { SetStatus(UiText.T("请先成功编译当前内容，再导出 PDF。", "Compile the current content successfully before exporting.")); return; }
        var data = _pdf; var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(_entry) }; InitializePicker(picker); picker.FileTypeChoices.Add(UiText.T("PDF 文档", "PDF document"), new List<string> { ".pdf" });
        var file = await picker.PickSaveFileAsync(); if (file != null) { await File.WriteAllBytesAsync(file.Path, data); SetStatus(UiText.T("PDF 已导出。", "PDF exported.")); }
    });
    private void FileFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) { if (_sources != null) BuildTree(); }
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => Preview.SetZoomPercent(Preview.ZoomPercent * 0.85);
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => Preview.SetZoomPercent(Preview.ZoomPercent * 1.15);
    private void Fit_Click(object sender, RoutedEventArgs e) => Preview.FitWidth();
    private void Problems_Click(object sender, RoutedEventArgs e) { ShowPage(Workspace); SidebarPicker.SelectedIndex = 4; _ = RefreshSidebarAsync(); }
    private void Find_Click(object sender, RoutedEventArgs e) => ShowFind();
    private void ShowPage(FrameworkElement page)
    { Welcome.Visibility = page == Welcome ? Visibility.Visible : Visibility.Collapsed; Workspace.Visibility = page == Workspace ? Visibility.Visible : Visibility.Collapsed; SettingsPage.Visibility = page == SettingsPage ? Visibility.Visible : Visibility.Collapsed; SyncChrome(); }
    private void Home_Click(object sender, RoutedEventArgs e) { RefreshRecent(); ShowPage(Welcome); }
    private void Back_Click(object sender, RoutedEventArgs e) => ShowPage(_root == null ? Welcome : Workspace);
    private async Task<bool> ConfirmUnsavedAsync()
    {
        var dirty = _buffers.Values.Where(b => b.Dirty).ToArray(); if (dirty.Length == 0) return true;
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "保存未保存的文档？", Content = string.Join("\n", dirty.Select(b => Path.GetFileName(b.Path))), PrimaryButtonText = "全部保存", SecondaryButtonText = "放弃修改", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Primary };
        var result = await ShowDialogAsync(dialog); if (result == ContentDialogResult.None) return false;
        if (result == ContentDialogResult.Primary) { try { foreach (var buffer in dirty) await buffer.SaveAsync(); } catch (Exception error) { SetStatus(error.Message); return false; } }
        return true;
    }
    private Task ShowMessageAsync(string title, string content) => ShowDialogAsync(new ContentDialog { XamlRoot = Root.XamlRoot, Title = title, Content = new TextBox { Text = UiText.Translate(content), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = Ui.D("DialogMaxHeight") }, CloseButtonText = "关闭" });
    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        if (_activeDialog != null) return ContentDialogResult.None;
        dialog.RequestedTheme = Root.ActualTheme;
        dialog.Title = UiText.Translate(dialog.Title?.ToString() ?? ""); dialog.PrimaryButtonText = UiText.Translate(dialog.PrimaryButtonText); dialog.SecondaryButtonText = UiText.Translate(dialog.SecondaryButtonText); dialog.CloseButtonText = UiText.Translate(dialog.CloseButtonText);
        _activeDialog = dialog;
        try { return await dialog.ShowAsync(); }
        finally { if (_activeDialog == dialog) _activeDialog = null; }
    }
    private async Task SafeAsync(Func<Task> action) { try { await action(); } catch (Exception error) { Busy.IsActive = false; SetStatus(error.Message); } }
    private async Task DisposeProjectAsync()
    { _sidebarWork.Cancel(); _compileError = _diagnostics = ""; _disposed = true; _revision++; _fileEpoch++; _compileOperation++; _compile.Cancel(); _navigation.Cancel(); _watcher?.Dispose(); _watcher = null; if (_session != null) await _session.DisposeAsync(); _session = null; Preview.Close(); }
    private async Task WaitForCompileAsync(string? expectedText = null)
    {
        for (var i = 0; i < 300; i++) { if (_pdfRevision == _revision && !Busy.IsActive && (expectedText == null || _current?.Text == expectedText)) return; await Task.Delay(50); }
        throw new TimeoutException("Compilation did not settle: " + Status.Text);
    }
    private async Task WaitForVisibleItemsAsync(ListViewBase list)
    {
        for (var i = 0; i < 80; i++) { Root.UpdateLayout(); if (list.ContainerFromIndex(0) != null) return; await Task.Delay(25); }
        throw new InvalidOperationException("Sidebar items were not realized by WinUI");
    }
    private async Task CaptureAsync(string path)
    {
            Root.UpdateLayout(); await Task.Delay(100);
            var backdrop = Root.Background;
            Root.Background = Ui.Brush(Root, "SolidBackgroundFillColorBaseBrush");
            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
            try { await bitmap.RenderAsync(Root); } finally { Root.Background = backdrop; }
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
            if (SidebarPicker.Items.Count != 5 || SidebarPicker.SelectedIndex != 0) throw new InvalidOperationException("Sidebar must default to project files with five choices");
            await OpenProjectAsync(root, confirmed: true);
            if (_watcher != null) _watcher.EnableRaisingEvents = false; // Smoke screenshots must not trigger asset recompilation.
            if (_pdf == null || _current == null) throw new InvalidOperationException("Initial compile failed: " + Status.Text);
            await VerifyFluentAsync(root);
            var offset = _current.Text.IndexOf("alpha", StringComparison.Ordinal); SelectSource(offset, 5);
            await ForwardAsync(); if (!Preview.HasNavigationMark) throw new InvalidOperationException("PDF navigation highlight is missing"); if (!Status.Text.Contains("已定位") && !Status.Text.Contains("Located")) throw new InvalidOperationException(Status.Text);
            var source = Path.Combine(root, "chapter.typ"); var child = await File.ReadAllTextAsync(source); var pos = SourceCoordinates.Position(SourceBuffer.Normalize(child), SourceBuffer.Normalize(child).IndexOf("alpha", StringComparison.Ordinal));
            var target = await _session!.ForwardAsync(source, pos.Line, pos.Column, CancellationToken.None);
            var point = target.Points.First(); Preview.GoTo(point); await InverseAsync(point.Page - 1, point.X + 0.5, point.Y - 1);
            if (_current?.Path != source) throw new InvalidOperationException("Cross-file inverse navigation failed: " + Status.Text);
            await Task.Delay(50); if (!Editor.HasNavigationMark) throw new InvalidOperationException("Source navigation highlight is missing");
            await CaptureAsync(Path.Combine(root, "navigation-feedback.png"));
            await Task.Delay(3100);
            if (Editor.HasNavigationMark || Preview.HasNavigationMark) throw new InvalidOperationException("Navigation feedback did not clear");
            await VerifyCompileTargetAsync();
            var previous = _current.Text; SelectSource(previous.Length, 0); Editor.SelectedText = "\nNative editing smoke test.\n";
            await WaitForCompileAsync(previous + "\nNative editing smoke test.\n"); if (_pdfRevision != _revision || !_current.Dirty) throw new InvalidOperationException("Live editing compile failed");
            Editor.Undo(); await WaitForCompileAsync(previous);
            if (_current.Text != previous) throw new InvalidOperationException("Native undo did not restore the original source");
            Editor.ApplyStyle();
            if (!Editor.HasHighlight) throw new InvalidOperationException("Typst native highlighting is missing");
            SelectSource(0, previous.Length); Editor.ToggleComment(); await WaitForCompileAsync();
            if (!Editor.Text.StartsWith("//")) throw new InvalidOperationException("Line comment was not inserted");
            Editor.Undo(); await WaitForCompileAsync(previous);
            if (Editor.Text != previous) throw new InvalidOperationException("Comment must undo in one action");
            await VerifyEditorCommandsAsync();
            Editor.ShowLineNumbers = false; Editor.ShowLineNumbers = true;
            var sidebarWidth = SidebarColumn.ActualWidth;
            ToggleSidebar_Click(this, new RoutedEventArgs()); Root.UpdateLayout();
            if (SidebarColumn.ActualWidth != 0 || RestoreSidebarButton.Visibility != Visibility.Visible) throw new InvalidOperationException("Sidebar did not hide");
            ToggleSidebar_Click(this, new RoutedEventArgs()); Root.UpdateLayout();
            if (Math.Abs(SidebarColumn.ActualWidth - sidebarWidth) > 1) throw new InvalidOperationException("Sidebar width was not restored");
            Preview.SetZoomPercent(125); Root.UpdateLayout();
            if (Math.Abs(Preview.ZoomPercent - 125) > 1) throw new InvalidOperationException("PDF percentage zoom failed");
            Preview.MovePage(Preview.PageCount - Preview.CurrentPage); await Task.Delay(80);
            if (Preview.CurrentPage != Preview.PageCount) throw new InvalidOperationException("PDF page jump failed");
            Preview.FitWidth();
            _editorRatio = 0.6; ApplySplit(); Root.UpdateLayout();
            if (EditorColumn.ActualWidth <= PreviewColumn.ActualWidth) throw new InvalidOperationException("Split ratio did not resize panes");
            ResetSplit_Click(this, new RoutedEventArgs());
            Preview.GoTo(point); await Preview.WaitForRenderAsync();
            await CaptureAsync(Path.Combine(root, "native-smoke.png"));
            Settings_Click(this, new RoutedEventArgs()); await Task.Delay(150);
            var settingsBounds = SettingsContent.TransformToVisual(Root).TransformBounds(new Windows.Foundation.Rect(0, 0, SettingsContent.ActualWidth, SettingsContent.ActualHeight));
            if (settingsBounds.Right > Root.ActualWidth + 1 || settingsBounds.Left < 0) throw new InvalidOperationException($"Settings overflow: left={settingsBounds.Left}, right={settingsBounds.Right}, window={Root.ActualWidth}"); await CaptureAsync(Path.Combine(root, "settings.png"));
            _language = "en"; ApplyLanguage(); _theme = "Light"; Root.RequestedTheme = ElementTheme.Light; UpdateCaption(); Settings_Click(this, new RoutedEventArgs()); await Task.Delay(150);
            if (MainMenu.Items[0].Title != "File" || !SettingsContent.Children.OfType<TextBlock>().Any(t => t.Text == "Settings")) throw new InvalidOperationException("English UI did not apply");
            await CaptureAsync(Path.Combine(root, "settings-light-en.png"));
            var scale = Root.XamlRoot.RasterizationScale; AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(960 * scale), (int)(650 * scale))); await Task.Delay(150);
            var narrow = SettingsContent.TransformToVisual(Root).TransformBounds(new Windows.Foundation.Rect(0, 0, SettingsContent.ActualWidth, SettingsContent.ActualHeight));
            if (narrow.Left < 0 || narrow.Right > Root.ActualWidth + 1) throw new InvalidOperationException("Narrow settings overflow");
            SaveSettings(); _language = "zh"; LoadSettings(); if (_language != "en" || !Editor.ShowLineNumbers) throw new InvalidOperationException("Preferences did not persist");
            _language = "zh"; _theme = "Dark"; Root.RequestedTheme = ElementTheme.Dark; ApplyLanguage(); UpdateCaption();
            Home_Click(this, new RoutedEventArgs()); await Task.Delay(150);
            if (Grid.GetRow(RecentArea) != 1) throw new InvalidOperationException("Narrow welcome did not stack");
            await CaptureAsync(Path.Combine(root, "welcome-narrow.png"));
            AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(1200 * scale), (int)(800 * scale))); await Task.Delay(150);
            if (Grid.GetColumn(RecentArea) != 1 || Math.Abs(WelcomeFolderButton.ActualWidth - 320) > 1 || Math.Abs(WelcomeFolderButton.ActualHeight - 40) > 1) throw new InvalidOperationException("Wide welcome dimensions failed");
            await CaptureAsync(Path.Combine(root, "welcome.png"));
            Back_Click(this, new RoutedEventArgs());
            SidebarPicker.SelectedIndex = 1; _projectQuery.Text = "alpha";
            for (var i = 0; i < 80 && (_searchResults.ItemsSource is not SidebarRow[] results || results.Length < 2); i++) await Task.Delay(25);
            if (_searchResults.ItemsSource is not SidebarRow[] hits || hits.Length < 2) throw new InvalidOperationException("Project search is missing cross-file results");
            if (_buffers.Values.Any(b => b.Dirty)) throw new InvalidOperationException("Switching panels must not modify source buffers");
            await WaitForVisibleItemsAsync(_searchResults); await CaptureAsync(Path.Combine(root, "sidebar-search.png"));
            if (_searchResults.ContainerFromIndex(0) == null) throw new InvalidOperationException("Search results must be visible");
            SidebarPicker.SelectedIndex = 2; await RefreshSidebarAsync();
            if (_outlineTree.RootNodes.Count < 2 || !_outlineTree.RootNodes.All(n => n.IsExpanded)) throw new InvalidOperationException("Compiled outline must be expanded");
            await CaptureAsync(Path.Combine(root, "sidebar-outline.png"));
            SidebarPicker.SelectedIndex = 3; _symbolQuery.Text = "integral"; FilterSymbols();
            if (_symbolList.Items.Count < 3) throw new InvalidOperationException("Symbol filtering is missing integral variants");
            await WaitForVisibleItemsAsync(_symbolList); await CaptureAsync(Path.Combine(root, "sidebar-symbols.png"));
            if (_symbolList.ContainerFromIndex(0) == null) throw new InvalidOperationException("Symbol cells must be visible");
            SidebarPicker.SelectedIndex = 4; await RefreshSidebarAsync(); await CaptureAsync(Path.Combine(root, "sidebar-problems.png"));
            _autoCompile = false; SelectSource(Editor.Text.Length, 0); Editor.SelectedText = "\n#set text(font: )"; await CompileNowAsync();
            for (var i = 0; i < 50 && _session!.Problems.Count == 0; i++) await Task.Delay(20);
            await RefreshSidebarAsync(); if (_problemList.Items.Count == 0) throw new InvalidOperationException("Errors must appear in the problems panel");
            await CaptureAsync(Path.Combine(root, "sidebar-errors.png"));
            Editor.Undo(); _autoCompile = true; await CompileNowAsync(); await WaitForCompileAsync(previous);
            for (var i = 0; i < 50 && _session!.Problems.Count > 0; i++) await Task.Delay(20);
            await RefreshSidebarAsync(); if (_problemList.Items.Count != 0) throw new InvalidOperationException("Fixed errors must disappear");
            SidebarPicker.SelectedIndex = 0;
            await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { success = true, architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), status = Status.Text, pages = point.Page, version = "0.2.3", rasterizationScale = Root.XamlRoot.RasterizationScale, uiCommands = "colored SVG decoding, folder state, entry emphasis, native badge, theme switching, line spacing, word count, timed bidirectional navigation feedback, explicit current-file compile, stable entry on browse/inverse, failed compile preserves PDF, rapid target changes, find/replace undo, format undo, sidebar restore, zoom/page jump" }));
        }
        catch (Exception error) { await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { success = false, error = error.ToString() })); }
        if (_args.Contains("--smoke-exit")) { _closing = true; await DisposeProjectAsync(); Close(); }
        // Without --smoke-exit, keep the owned test window open for visual inspection.
    }
}
