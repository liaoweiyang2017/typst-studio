using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Text;
using Windows.System;
using static TypstStudio.App.UiText;
namespace TypstStudio.App;
public sealed partial class MainWindow
{
    private string? _countedText;
    private bool _editorToolsInitialized;
    private int _characterCount, _wordCount;
    private void InitializeEditorTools()
    {
        Ui.Describe(ForwardButton, T("定位到 PDF (Ctrl+Shift+J)", "Go to PDF (Ctrl+Shift+J)"));
        Ui.Describe(FindQuery, T("查找 (Ctrl+F)", "Find (Ctrl+F)")); Ui.Describe(ReplaceQuery, T("替换文本", "Replacement text"));
        FindActions.Children.Clear();
        FindActions.Children.Add(Ui.IconButton("\uE70E", T("上一个", "Previous match"), (_, _) => FindInEditor(false)));
        FindActions.Children.Add(Ui.IconButton("\uE70D", T("下一个", "Next match"), (_, _) => FindInEditor(true)));
        FindActions.Children.Add(Ui.IconButton("\uE8AC", T("显示替换", "Show replace"), (_, _) => ReplaceRow.Visibility = ReplaceRow.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible));
        FindActions.Children.Add(Ui.IconButton("\uE711", T("关闭查找 (Esc)", "Close find (Esc)"), (_, _) => CloseFind()));
        FormatToolbar.Children.Clear();
        void Format(string data, string title, Action action)
        {
            var icon = (PathIcon)Microsoft.UI.Xaml.Markup.XamlReader.Load($"<PathIcon xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Width='16' Height='16' Data='{data}'/>");
            var b = new AppBarButton { Icon = icon, Label = title, Style = Ui.Style("StudioFormatButton") };
            Ui.Describe(b, title); b.Click += (_, _) => { if (_current != null && !_opening) action(); };
            FormatToolbar.Children.Add(b);
        }
        Format("F0 M4 2H8.2C10.6 2 12 3.1 12 5.1C12 6.3 11.4 7.1 10.4 7.5C11.8 7.9 12.5 8.9 12.5 10.4C12.5 12.7 10.8 14 8.3 14H4Z M5.8 3.6V6.9H8C9.5 6.9 10.2 6.3 10.2 5.2C10.2 4.1 9.4 3.6 8 3.6Z M5.8 8.5V12.4H8.2C9.9 12.4 10.7 11.7 10.7 10.4C10.7 9.2 9.9 8.5 8.1 8.5Z", T("粗体", "Bold"), () => WrapSelection("*", "*"));
        Format("M8 2H13V3.5H11.2L7.4 12.5H10V14H4V12.5H5.6L9.4 3.5H8Z", T("斜体", "Italic"), () => WrapSelection("_", "_"));
        Format("M3.5 2H5V8.5C5 10.5 6 11.5 8 11.5S11 10.5 11 8.5V2H12.5V8.5C12.5 11.4 10.9 13 8 13S3.5 11.4 3.5 8.5Z M3.5 14H12.5V15.3H3.5Z", T("下划线", "Underline"), () => WrapSelection("#underline[", "]"));
        Format("M2 2.5H14V3.8H2Z M4 6.4H12V7.7H4Z M2 10.3H14V11.6H2Z M4 14.2H12V15.5H4Z", T("居中对齐", "Center alignment"), () => WrapSelection("#align(center)[", "]"));
        Format("M2 2A1 1 0 1 0 2 4A1 1 0 1 0 2 2Z M2 7A1 1 0 1 0 2 9A1 1 0 1 0 2 7Z M2 12A1 1 0 1 0 2 14A1 1 0 1 0 2 12Z M5 2.4H15V3.6H5Z M5 7.4H15V8.6H5Z M5 12.4H15V13.6H5Z", T("无序列表", "Bullet list"), () => PrefixLines("- "));
        Format("M1 2L2.5 1H3.5V5H2.3V2.5L1.6 3Z M1 7.2C1.5 5.9 3.8 5.8 3.8 7.4C3.8 8.2 3 8.8 2.3 9.4H4V10.4H1V9.5C1.7 8.8 2.6 8.1 2.6 7.5C2.6 7 1.9 7 1.8 7.6Z M1 11.5H3.8V12.4L2.9 13C4.4 13.4 4 15.5 2.4 15.5C1.6 15.5 1 15.1.8 14.5L1.8 14.1C2 14.7 2.9 14.7 2.9 14.1C2.9 13.7 2.4 13.5 1.7 13.7V12.9L2.6 12.4H1Z M6 2.4H15V3.6H6Z M6 7.4H15V8.6H6Z M6 12.4H15V13.6H6Z", T("编号列表", "Numbered list"), () => PrefixLines("+ "));
        Format("M3 2H13V3.5H5.4L9.5 7.6L5.4 12.5H13V14H3V12.6L7.5 7.7L3 3.4Z", T("公式", "Equation"), () => WrapSelection("$ ", " $"));
        SidebarToolbar.Children.Clear();
        SidebarToolbar.Children.Add(Ui.IconButton("\uE8A5", T("新建文件", "New file"), NewFileInProject_Click));
        SidebarToolbar.Children.Add(Ui.IconButton("\uE8F4", T("新建文件夹", "New folder"), NewFolder_Click));
        SidebarToolbar.Children.Add(Ui.IconButton("\uE72C", T("刷新工程", "Refresh project"), async (_, _) => await SafeAsync(RefreshProjectAsync)));
        SidebarToolbar.Children.Add(Ui.IconButton("\uE713", T("设置", "Settings"), Settings_Click));
        FindOverlay.Shadow = new ThemeShadow(); FindOverlay.Translation = new System.Numerics.Vector3(0, 0, (float)Ui.D("PaperElevation"));
        if (_editorToolsInitialized) return; _editorToolsInitialized = true;
        Root.PreviewKeyDown += (_, e) => { if (e.Key == VirtualKey.Escape && FindOverlay.Visibility == Visibility.Visible) { CloseFind(); e.Handled = true; } };
        EditorSurface.SizeChanged += (_, _) => SizeFindOverlay();
        WrapMenuItem.IsChecked = Editor.WordWrap;
        AddShortcut(VirtualKey.F, VirtualKeyModifiers.Control, () => { ShowFind(); return Task.CompletedTask; });
        AddShortcut(VirtualKey.B, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, () => { ToggleSidebar_Click(this, new RoutedEventArgs()); return Task.CompletedTask; });
    }
    private void SizeFindOverlay() => FindOverlay.Width = Math.Max(0, Math.Min(Ui.D("FindOverlayWidth"), EditorSurface.ActualWidth - Ui.D("Space24")));
    private void ShowFind() { if (_current == null) return; ShowPage(Workspace); FindOverlay.Visibility = Visibility.Visible; SizeFindOverlay(); FindQuery.Focus(FocusState.Programmatic); FindQuery.SelectAll(); }
    private void CloseFind() { FindOverlay.Visibility = Visibility.Collapsed; Editor.Focus(FocusState.Programmatic); }
    private void FindQuery_KeyDown(object sender, KeyRoutedEventArgs e) { if (e.Key == VirtualKey.Escape) { CloseFind(); e.Handled = true; } else if (e.Key == VirtualKey.Enter) { FindInEditor(true); e.Handled = true; } }
    private int FindInEditor(bool forward)
    {
        var query = FindQuery.Text; if (_current == null || string.IsNullOrEmpty(query)) return -1;
        var text = Editor.Text; var start = Math.Clamp(Editor.SelectionStart + (forward ? Editor.SelectionLength : -1), 0, text.Length);
        var index = forward ? text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase) : start < text.Length ? text.LastIndexOf(query, start, StringComparison.OrdinalIgnoreCase) : text.LastIndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0) index = forward ? text.IndexOf(query, StringComparison.OrdinalIgnoreCase) : text.LastIndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index >= 0) { SelectSource(index, query.Length); FindResult.Text = T("已找到匹配内容", "Match found"); } else FindResult.Text = T("没有匹配内容", "No matches");
        return index;
    }
    private void ReplaceOne_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null || FindQuery.Text.Length == 0) return;
        if (!Editor.SelectedText.Equals(FindQuery.Text, StringComparison.OrdinalIgnoreCase) && FindInEditor(true) < 0) return;
        Editor.SelectedText = ReplaceQuery.Text; FindInEditor(true);
    }
    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null || FindQuery.Text.Length == 0) return;
        var text = Editor.Text; var replacement = text.Replace(FindQuery.Text, ReplaceQuery.Text, StringComparison.OrdinalIgnoreCase);
        if (text == replacement) { FindResult.Text = T("没有需要替换的内容", "Nothing to replace"); return; }
        var caret = Editor.SelectionStart; Editor.ReplaceDocumentAsEdit(replacement, caret); FindResult.Text = T("已全部替换，可撤销", "Replaced all; undo is available");
    }
    private void WrapSelection(string prefix, string suffix)
    {
        var text = Editor.SelectedText;
        if (text.StartsWith(prefix, StringComparison.Ordinal) && text.EndsWith(suffix, StringComparison.Ordinal) && text.Length >= prefix.Length + suffix.Length) Editor.SelectedText = text[prefix.Length..^suffix.Length];
        else Editor.SelectedText = prefix + (text.Length == 0 ? (prefix.StartsWith('$') ? "x" : T("文本", "text")) : text) + suffix;
        Editor.Focus(FocusState.Programmatic);
    }
    private void PrefixLines(string prefix)
    {
        var text = Editor.Text; var start = Editor.SelectionStart == 0 ? 0 : text.LastIndexOf('\n', Editor.SelectionStart - 1) + 1;
        var end = text.IndexOf('\n', Math.Min(text.Length, Editor.SelectionStart + Editor.SelectionLength)); if (end < 0) end = text.Length;
        Editor.Select(start, end - start); Editor.SelectedText = string.Join('\n', text[start..end].Split('\n').Select(line => prefix + line)); Editor.Focus(FocusState.Programmatic);
    }
    private void UpdateEditorPosition()
    {
        var text = Editor.Text;
        if (!ReferenceEquals(_countedText, text)) { _countedText = text; _characterCount = text.EnumerateRunes().Count(r => !Rune.IsWhiteSpace(r)); _wordCount = CountWords(text); }
        var p = EditorPosition(); PositionText.Text = T($"行 {p.Line + 1}, 列 {p.Column + 1} · {_characterCount} 字", $"Ln {p.Line + 1}, Col {p.Column + 1} · {_characterCount} chars");
        WordCountText.Text = T($"{_wordCount} 字词", $"{_wordCount} words");
    }
    // Count CJK ideographs individually and other letter/digit runs as words.
    private static int CountWords(string text)
    {
        int count = 0; bool inWord = false;
        foreach (var rune in text.EnumerateRunes()) {
            bool cjk = rune.Value is >= 0x3400 and <= 0x9FFF or >= 0xF900 and <= 0xFAFF or >= 0x20000 and <= 0x323AF;
            bool word = Rune.IsLetterOrDigit(rune);
            if (cjk || word && !inWord) count++;
            inWord = word && !cjk;
        }
        return count;
    }
    private void Wrap_Click(object sender, RoutedEventArgs e) { Editor.WordWrap = WrapMenuItem.IsChecked; SaveSettings(); }
    private async Task RefreshProjectAsync()
    { if (_root == null) return; var root = _root; _sources = await Task.Run(() => TypstStudio.Core.ProjectFiles.Sources(root)); if (root == _root) BuildTree(); }
    private async void NewFileInProject_Click(object sender, RoutedEventArgs e) => await SafeAsync(() => CreateProjectItemAsync(false));
    private async void NewFolder_Click(object sender, RoutedEventArgs e) => await SafeAsync(() => CreateProjectItemAsync(true));
    private async Task CreateProjectItemAsync(bool folder)
    {
        if (_root == null || _opening) return;
        var root = _root; var name = new TextBox { Text = folder ? "folder" : "untitled.typ" };
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = folder ? T("新建文件夹", "New folder") : T("新建文件", "New file"), Content = name, PrimaryButtonText = T("创建", "Create"), CloseButtonText = T("取消", "Cancel") };
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary || root != _root) return;
        var value = name.Text.Trim();
        if (value.Length == 0 || value is "." or ".." || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.EndsWith('.') || value.EndsWith(' ')) throw new IOException(T("请输入有效名称。", "Enter a valid name."));
        var path = Path.Combine(root, value); if (File.Exists(path) || Directory.Exists(path)) throw new IOException(T("同名项目已存在。", "An item with this name already exists."));
        if (folder) Directory.CreateDirectory(path); else { using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
        await RefreshProjectAsync();
        if (!folder && Path.GetExtension(path).Equals(".typ", StringComparison.OrdinalIgnoreCase)) { if (_session == null) await OpenProjectAsync(root, path); else await OpenBufferAsync(path); }
    }
}
