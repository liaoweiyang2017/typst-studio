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
    private int _characterCount;
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
        void Format(string glyph, string title, Action action, bool math = false)
        {
            var b = Ui.IconButton(glyph, title, (_, _) => { if (_current != null && !_opening) action(); });
            b.Width = b.Height = b.MinWidth = b.MinHeight = Ui.D("FormatButtonSize");
            if (glyph == "\uEA37") b.Content = new TextBlock { Text = "1.", FontFamily = (FontFamily)Application.Current.Resources["StudioUIFont"], FontSize = Ui.D("FontIcon") };
            if (math && b.Content is FontIcon icon) icon.FontFamily = (FontFamily)Application.Current.Resources["StudioMathFont"];
            FormatToolbar.Children.Add(b);
        }
        Format("\uE8DD", T("粗体", "Bold"), () => WrapSelection("*", "*"));
        Format("\uE8DB", T("斜体", "Italic"), () => WrapSelection("_", "_"));
        Format("\uE8DC", T("下划线", "Underline"), () => WrapSelection("#underline[", "]"));
        Format("\uE8E3", T("居中对齐", "Center alignment"), () => WrapSelection("#align(center)[", "]"));
        Format("\uE8FD", T("无序列表", "Bullet list"), () => PrefixLines("- "));
        Format("\uEA37", T("编号列表", "Numbered list"), () => PrefixLines("+ "));
        Format("∑", T("公式", "Equation"), () => WrapSelection("$ ", " $"), math: true);
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
        if (!ReferenceEquals(_countedText, text)) { _countedText = text; _characterCount = text.EnumerateRunes().Count(r => !Rune.IsWhiteSpace(r)); }
        var p = EditorPosition(); PositionText.Text = T($"行 {p.Line + 1}, 列 {p.Column + 1} · {_characterCount} 字", $"Ln {p.Line + 1}, Col {p.Column + 1} · {_characterCount} chars");
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
