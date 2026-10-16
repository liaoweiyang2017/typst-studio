using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Text.Json;
using Windows.System;
using static TypstStudio.App.UiText;
namespace TypstStudio.App;
public sealed partial class MainWindow
{
    private string _language = "System", _commentShortcut = "Ctrl+/", _pdfMode = "Standard";
    private bool _autoCompile = true;
    private int _compileDelay = 160;
    private double _editorRatio = 0.5;
    private KeyboardAccelerator? _commentAccelerator;
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        SettingsContent.Children.Clear();
        var back = new Button { Content = T("← 返回", "← Back"), Padding = new Thickness(10, 4, 10, 4) }; back.Click += Back_Click; SettingsContent.Children.Add(back);
        SettingsContent.Children.Add(new TextBlock { Text = T("设置", "Settings"), FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 6) });
        void Heading(string title) => SettingsContent.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 18, 0, 6) });
        void Row(Symbol icon, string title, string description, FrameworkElement? control = null)
        {
            var grid = new Grid { Padding = new Thickness(16, 12, 16, 12), ColumnSpacing = 16, MinHeight = 68 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(new SymbolIcon(icon) { Width = 20 }); var text = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center }; text.Children.Add(new TextBlock { Text = title, FontSize = 14 }); text.Children.Add(new TextBlock { Text = description, FontSize = 12, Opacity = 0.65, TextWrapping = TextWrapping.Wrap }); Grid.SetColumn(text, 1); grid.Children.Add(text);
            if (control != null) { Grid.SetColumn(control, 2); control.VerticalAlignment = VerticalAlignment.Center; grid.Children.Add(control); }
            var card = (Border)Microsoft.UI.Xaml.Markup.XamlReader.Load("<Border xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Background='{ThemeResource CardBackgroundFillColorDefaultBrush}' BorderBrush='{ThemeResource CardStrokeColorDefaultBrush}' BorderThickness='1' CornerRadius='6'/>"); card.Child = grid; SettingsContent.Children.Add(card);
        }
        ComboBox Choice(string[] values, int selected, Action<int> change)
        { var box = new ComboBox { Width = 170, ItemsSource = values, SelectedIndex = selected }; box.SelectionChanged += (_, _) => change(box.SelectedIndex); return box; }
        ToggleSwitch Toggle(bool value, Action<bool> change)
        { var toggle = new ToggleSwitch { IsOn = value, OnContent = T("开启", "On"), OffContent = T("关闭", "Off"), MinWidth = 100 }; toggle.Toggled += (_, _) => change(toggle.IsOn); return toggle; }
        Heading(T("个性化", "Personalization"));
        Row(Symbol.World, T("界面语言", "Language"), T("选择中英文界面，更改立即生效。", "Changes apply immediately."), Choice([T("跟随系统", "Use system setting"), "English", "简体中文"], _language == "en" ? 1 : _language == "zh" ? 2 : 0, i => { _language = i == 1 ? "en" : i == 2 ? "zh" : "System"; ApplyLanguage(); SaveSettings(); DispatcherQueue.TryEnqueue(() => Settings_Click(this, new RoutedEventArgs())); }));
        Row(Symbol.Setting, T("应用主题", "Application theme"), T("标题栏、菜单与工作区保持一致。", "Applies to title bar, menus, and workspace."), Choice([T("跟随系统", "Use system setting"), T("浅色", "Light"), T("深色", "Dark")], _theme == "Light" ? 1 : _theme == "Dark" ? 2 : 0, i => { _theme = i == 1 ? "Light" : i == 2 ? "Dark" : "Default"; Root.RequestedTheme = Enum.Parse<ElementTheme>(_theme); UpdateCaption(); SaveSettings(); }));
        Heading(T("编辑器", "Editor"));
        var font = new NumberBox { Width = 170, Minimum = 11, Maximum = 24, SmallChange = 1, Value = Editor.FontSize, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact }; font.ValueChanged += (_, _) => { if (double.IsFinite(font.Value)) { Editor.FontSize = font.Value; SaveSettings(); } };
        Row(Symbol.Font, T("字号", "Font size"), T("Typst 源码使用等宽字体和语法高亮。", "Monospaced source with Typst syntax highlighting."), font);
        Row(Symbol.List, T("显示行号", "Line numbers"), T("在源码左侧显示行号。", "Show line numbers alongside the source."), Toggle(Editor.ShowLineNumbers, value => { Editor.ShowLineNumbers = value; SaveSettings(); }));
        Row(Symbol.AlignLeft, T("自动换行", "Word wrap"), T("将长行折到下一视觉行，不改变源文件。", "Wrap long lines without modifying the file."), Toggle(Editor.WordWrap, value => { Editor.WordWrap = value; SaveSettings(); }));
        Row(Symbol.Edit, T("注释快捷键", "Comment shortcut"), T("对当前行或选中的多行切换 // 注释。", "Toggle // comments on the current or selected lines."), Choice(["Ctrl+/", "Ctrl+Shift+C", "Ctrl+Alt+C"], _commentShortcut == "Ctrl+Shift+C" ? 1 : _commentShortcut == "Ctrl+Alt+C" ? 2 : 0, i => { _commentShortcut = i == 1 ? "Ctrl+Shift+C" : i == 2 ? "Ctrl+Alt+C" : "Ctrl+/"; SetCommentShortcut(); SaveSettings(); }));
        Heading(T("编译", "Compilation"));
        Row(Symbol.Play, T("自动编译", "Automatic compilation"), T("编辑后在后台更新 PDF，保留主文档。", "Update the main PDF in the background after editing."), Toggle(_autoCompile, value => { _autoCompile = value; if (value && _current != null) ScheduleCompile(); SaveSettings(); }));
        Row(Symbol.Clock, T("编译等待", "Typing delay"), T("减少连续输入期间的重复编译。", "Avoid repeated compilation while typing."), Choice(["160 ms", "300 ms", "500 ms"], _compileDelay == 500 ? 2 : _compileDelay == 300 ? 1 : 0, i => { _compileDelay = i == 2 ? 500 : i == 1 ? 300 : 160; SaveSettings(); }));
        Heading(T("PDF 与布局", "PDF and layout"));
        Row(Symbol.Document, T("PDF 阅读模式", "PDF appearance"), T("仅调整屏幕显示，导出的 PDF 不变。", "Display only; exported PDF files are unchanged."), Choice([T("原始颜色", "Original colors"), T("夜间", "Night"), T("护眼绿", "Soft green")], _pdfMode == "Night" ? 1 : _pdfMode == "Green" ? 2 : 0, i => { _pdfMode = i == 1 ? "Night" : i == 2 ? "Green" : "Standard"; Preview.SetColorMode(_pdfMode); SaveSettings(); }));
        var reset = new Button { Content = T("恢复均分", "Equal widths"), Width = 170 }; reset.Click += ResetSplit_Click;
        Row(Symbol.TwoPage, T("源码与 PDF 分栏", "Editor / PDF split"), T("拖动中间分隔线调整宽度，自动记住比例。", "Drag the divider; the ratio is remembered."), reset);
        Row(Symbol.Find, T("双向定位", "Source mapping"), T("源码 Ctrl+Shift+J；PDF 中 Ctrl+单击公式或选中文字后 Ctrl+Alt+J。", "Source: Ctrl+Shift+J. PDF: Ctrl+click, or select text and press Ctrl+Alt+J."));
        Heading(T("关于", "About")); Row(Symbol.Help, "Typst Studio 0.1.9", T("WinUI 3 原生界面 · Tinymist · Scintilla · PDFium", "Native WinUI 3 · Tinymist · Scintilla · PDFium"));
        ShowPage(SettingsPage);
    }
    private void ApplyLanguage()
    {
        UiText.English = _language == "en" || (_language == "System" && !System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase));
        UiText.Apply(Root); UiText.Apply(MainMenu); RefreshSidebarLabels(); RefreshRecent(); UpdateTitle(); Editor_SelectionChanged(this, new RoutedEventArgs()); SetStatus(T("就绪。", "Ready."));
    }
    private void SetCommentShortcut()
    {
        if (_commentAccelerator != null) Root.KeyboardAccelerators.Remove(_commentAccelerator);
        _commentAccelerator = null;
        // OEM slash has no named VirtualKey and WinUI cannot generate its accelerator label.
        // Handle this chord from the routed key event instead of a KeyboardAccelerator.
        if (_commentShortcut == "Ctrl+/") return;
        _commentAccelerator = new KeyboardAccelerator { Key = _commentShortcut == "Ctrl+/" ? (VirtualKey)191 : VirtualKey.C, Modifiers = VirtualKeyModifiers.Control | (_commentShortcut == "Ctrl+Shift+C" ? VirtualKeyModifiers.Shift : _commentShortcut == "Ctrl+Alt+C" ? VirtualKeyModifiers.Menu : VirtualKeyModifiers.None) };
        _commentAccelerator.Invoked += (_, e) => { if (Workspace.Visibility == Visibility.Visible) { Editor.ToggleComment(); e.Handled = true; } }; Root.KeyboardAccelerators.Add(_commentAccelerator);
    }
    private void CommentKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_commentShortcut != "Ctrl+/" || Workspace.Visibility != Visibility.Visible || (int)e.Key != 191) return;
        var control = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (control) { Editor.ToggleComment(); e.Handled = true; }
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, uint attribute, ref int value, int size);
    private void UpdateCaption()
    {
        var dark = Root.ActualTheme == ElementTheme.Dark; int enabled = dark ? 1 : 0;
        DwmSetWindowAttribute(WinRT.Interop.WindowNative.GetWindowHandle(this), 20, ref enabled, sizeof(int));

    }
    private void Undo_Click(object sender, RoutedEventArgs e) => Editor.Undo();
    private void Redo_Click(object sender, RoutedEventArgs e) => Editor.Redo();
    private void Comment_Click(object sender, RoutedEventArgs e) => Editor.ToggleComment();
    private void SidebarDivider_DragDelta(object sender, DragDeltaEventArgs e) => SidebarColumn.Width = new GridLength(Math.Clamp(SidebarColumn.ActualWidth + e.HorizontalChange, 160, Math.Max(160, Workspace.ActualWidth - 540)));
    private void DocumentDivider_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var total = EditorColumn.ActualWidth + PreviewColumn.ActualWidth; if (total <= 520) return;
        _editorRatio = Math.Clamp((EditorColumn.ActualWidth + e.HorizontalChange) / total, 260 / total, 1 - 260 / total); ApplySplit();
    }
    private void ApplySplit() { EditorColumn.Width = new GridLength(_editorRatio, GridUnitType.Star); PreviewColumn.Width = new GridLength(1 - _editorRatio, GridUnitType.Star); }
    private void Divider_DragCompleted(object sender, DragCompletedEventArgs e) => SaveSettings();
    private void ResetSplit_Click(object sender, RoutedEventArgs e) { _editorRatio = 0.5; SidebarColumn.Width = new GridLength(200); ApplySplit(); SaveSettings(); }
    private void PreviousPage_Click(object sender, RoutedEventArgs e) => Preview.MovePage(-1);
    private void NextPage_Click(object sender, RoutedEventArgs e) => Preview.MovePage(1);
    private void FitPage_Click(object sender, RoutedEventArgs e) => Preview.FitPage();
    private void LoadSettings()
    {
        try
        {
            using var settings = JsonDocument.Parse(File.ReadAllText(_settingsPath)); var s = settings.RootElement;
            string Str(string key, string fallback) => s.TryGetProperty(key, out var value) ? value.GetString() ?? fallback : fallback;
            bool Flag(string key, bool fallback) => s.TryGetProperty(key, out var value) ? value.GetBoolean() : fallback;
            double Num(string key, double fallback) => s.TryGetProperty(key, out var value) && value.TryGetDouble(out var number) && double.IsFinite(number) ? number : fallback;
            _theme = Str("theme", "Default"); if (_theme is not ("Dark" or "Light")) _theme = "Default";
            _language = Str("language", "System"); _commentShortcut = Str("commentShortcut", "Ctrl+/"); _pdfMode = Str("pdfMode", "Standard");
            Root.RequestedTheme = Enum.Parse<ElementTheme>(_theme); Editor.FontSize = Math.Clamp(Num("fontSize", 14), 11, 24); Editor.ShowLineNumbers = Flag("lineNumbers", true); Editor.WordWrap = Flag("wordWrap", false);
            _autoCompile = Flag("autoCompile", true); _compileDelay = (int)Math.Clamp(Num("compileDelay", 160), 100, 1000); _editorRatio = Math.Clamp(Num("editorRatio", 0.5), 0.2, 0.8); SidebarColumn.Width = new GridLength(Math.Clamp(Num("sidebarWidth", 200), 160, 360));
            if (s.TryGetProperty("recent", out var recent)) _recent.AddRange(recent.EnumerateArray().Select(p => p.GetString()!).Where(Directory.Exists).Take(10));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException) { }
        var initial = Array.IndexOf(_args, "--initial-language"); if (!File.Exists(_settingsPath) && initial >= 0 && initial + 1 < _args.Length) _language = _args[initial + 1] == "zh" ? "zh" : "en";
        ApplySplit(); Preview.SetColorMode(_pdfMode); SetCommentShortcut();
    }
    private void SaveSettings()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!); File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new { theme = _theme, language = _language, fontSize = Editor.FontSize, lineNumbers = Editor.ShowLineNumbers, wordWrap = Editor.WordWrap, commentShortcut = _commentShortcut, autoCompile = _autoCompile, compileDelay = _compileDelay, pdfMode = _pdfMode, editorRatio = _editorRatio, sidebarWidth = SidebarColumn.Width.Value, recent = _recent })); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { SetStatus(error.Message); }
    }
}
