using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using TypstStudio.Core;
using static TypstStudio.App.UiText;
namespace TypstStudio.App;
public sealed partial class MainWindow
{
    private readonly AutoSuggestBox _projectQuery = new() { QueryIcon = new SymbolIcon(Symbol.Find) };
    private readonly CheckBox _matchCase = new();
    private readonly TextBlock _searchStatus = new() { TextWrapping = TextWrapping.Wrap, Opacity = Ui.D("SecondaryOpacity"), FontSize = Ui.D("FontCaption") };
    private readonly ListView _searchResults = new() { IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBlock _outlineStatus = new() { TextWrapping = TextWrapping.Wrap, Opacity = Ui.D("SecondaryOpacity"), FontSize = Ui.D("FontCaption") };
    private readonly TreeView _outlineTree = new() { SelectionMode = TreeViewSelectionMode.Single };
    private readonly AutoSuggestBox _symbolQuery = new() { QueryIcon = new SymbolIcon(Symbol.Find) };
    private readonly GridView _symbolList = new() { IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.None };
    private readonly TextBlock _problemStatus = new() { TextWrapping = TextWrapping.Wrap, Opacity = Ui.D("SecondaryOpacity"), FontSize = Ui.D("FontCaption"), MaxLines = 6, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly ListView _problemList = new() { IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.Single };
    private readonly Button _retryCompile = new();
    private readonly List<Grid> _sidebarPages = [];
    private CancellationTokenSource _sidebarWork = new();
    private string _compileError = "";
    private string? _lastSymbolFilter;
    private byte[]? _outlinePdf;
    private string? _outlineSource;
    private (string? Root, string Query, bool MatchCase, int Revision)? _searchKey;
    private string? _problemSignature;
    [Microsoft.UI.Xaml.Data.Bindable]
    public sealed record SidebarRow(string Title, string Detail, object Value);
    [Microsoft.UI.Xaml.Data.Bindable]
    public sealed record SymbolRow(string Glyph, string Name);
    private sealed record OutlineRow(string Title, PdfPoint? Destination, int? Offset = null)
    { public override string ToString() => Title; }
    private static Grid SidebarPage(FrameworkElement header, FrameworkElement body, FrameworkElement? footer = null)
    {
        var grid = new Grid { RowSpacing = Ui.D("Gap8") }; grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.Children.Add(header); Grid.SetRow(body, 1); grid.Children.Add(body); if (footer != null) { Grid.SetRow(footer, 2); grid.Children.Add(footer); } return grid;
    }
    private void InitializeSidebar()
    {
        var rowTemplate = (DataTemplate)XamlReader.Load("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel Padding='{ThemeResource Inset4_8}' Spacing='{ThemeResource Space4}' ToolTipService.ToolTip='{Binding Detail}'><TextBlock Text='{Binding Title}' FontWeight='SemiBold' FontSize='{ThemeResource FontCaption}' TextWrapping='Wrap'/><TextBlock Text='{Binding Detail}' FontSize='{ThemeResource FontCaption}' Opacity='{ThemeResource SecondaryOpacity}' TextWrapping='Wrap' MaxLines='3' TextTrimming='CharacterEllipsis'/></StackPanel></DataTemplate>");
        _searchResults.ItemTemplate = rowTemplate; _problemList.ItemTemplate = rowTemplate;
        var searchHeader = new StackPanel { Spacing = Ui.D("Gap4") }; searchHeader.Children.Add(_projectQuery); searchHeader.Children.Add(_matchCase); searchHeader.Children.Add(_searchStatus);
        _sidebarPages.Add(SidebarPage(searchHeader, _searchResults));
        _sidebarPages.Add(SidebarPage(_outlineStatus, _outlineTree));
        _symbolList.ItemTemplate = (DataTemplate)XamlReader.Load("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel Width='{ThemeResource SymbolCellWidth}' Padding='{ThemeResource Inset4_8}' ToolTipService.ToolTip='{Binding Name}'><TextBlock Text='{Binding Glyph}' FontFamily='{ThemeResource StudioMathFont}' FontSize='{ThemeResource FontSymbol}' HorizontalAlignment='Center'/><TextBlock Text='{Binding Name}' FontSize='{ThemeResource FontCaption}' TextTrimming='CharacterEllipsis' HorizontalAlignment='Center'/></StackPanel></DataTemplate>");
        _sidebarPages.Add(SidebarPage(_symbolQuery, _symbolList));
        _sidebarPages.Add(SidebarPage(_problemStatus, _problemList, _retryCompile));
        _projectQuery.TextChanged += (_, _) => { if (SidebarPicker.SelectedIndex == 1) _ = RefreshSidebarAsync(); };
        _matchCase.Checked += (_, _) => { if (SidebarPicker.SelectedIndex == 1) _ = RefreshSidebarAsync(); }; _matchCase.Unchecked += (_, _) => { if (SidebarPicker.SelectedIndex == 1) _ = RefreshSidebarAsync(); };
        _symbolQuery.TextChanged += (_, _) => FilterSymbols();
        _searchResults.ItemClick += async (_, e) => { if (e.ClickedItem is SidebarRow { Value: SearchHit hit }) await SafeAsync(async () => { await OpenBufferAsync(hit.Path); if (_current?.Path == hit.Path) { SelectSource(Math.Min(hit.Offset, _current.Text.Length), Math.Min(hit.Length, Math.Max(0, _current.Text.Length - hit.Offset))); } }); };
        _outlineTree.ItemInvoked += (_, e) => { if ((e.InvokedItem is TreeViewNode node ? node.Content : e.InvokedItem) is OutlineRow item) { if (item.Destination != null) Preview.GoTo(item.Destination); else if (item.Offset is int offset && _current != null) SelectSource(Math.Min(offset, _current.Text.Length), 0); } };
        _symbolList.ItemClick += (_, e) => { if (e.ClickedItem is SymbolRow item && _current != null) { Editor.SelectedText = MathSymbols.Insertion(Editor.Text, Editor.SelectionStart, item.Name); Editor.Focus(FocusState.Programmatic); } };
        _problemList.ItemClick += async (_, e) => { if (e.ClickedItem is SidebarRow { Value: CompilerProblem problem }) await SafeAsync(async () => { await OpenBufferAsync(problem.Path); if (_current?.Path == problem.Path) SelectSource(ProjectSearch.LineOffset(_current.Text, problem.Line, problem.Column), 0); }); };
        _retryCompile.Click += Compile_Click;
        RefreshSidebarLabels(); SidebarPicker.SelectedIndex = 0;
    }
    private void RefreshSidebarLabels()
    {
        if (SidebarPicker == null || _sidebarPages.Count == 0) return;
        string[] labels = [T("工程文件", "Project files"), T("工程搜索", "Search"), T("文档大纲", "Outline"), T("数学符号", "Symbols"), T("编译问题", "Problems")];
        string[] glyphs = ["\uE8B7", "\uE721", "\uE8FD", "\uE94F", "\uE7BA"];
        SidebarPicker.SetItems(labels, glyphs);
        _searchKey = null; _problemSignature = null; _outlinePdf = null; _outlineSource = null;
        _projectQuery.PlaceholderText = T("搜索工程源码", "Search project source"); _matchCase.Content = T("区分大小写", "Match case"); _symbolQuery.PlaceholderText = T("搜索符号或名称", "Search symbols or names"); _retryCompile.Content = T("重新编译", "Compile again");
        FilterSymbols(); _ = RefreshSidebarAsync();
    }
    private void SidebarPicker_SelectionChanged(object sender, EventArgs e)
    {
        if (SidebarTools == null || _sidebarPages.Count == 0) return;
        var mode = Math.Max(0, SidebarPicker.SelectedIndex);
        FileFilter.Visibility = Files.Visibility = mode == 0 ? Visibility.Visible : Visibility.Collapsed;
        SidebarTools.Visibility = mode == 0 ? Visibility.Collapsed : Visibility.Visible;
        SidebarTools.Content = mode == 0 ? null : _sidebarPages[mode - 1];
        _ = RefreshSidebarAsync();
    }
    private void FilterSymbols()
    {
        var query = _symbolQuery.Text ?? "";
        _symbolList.IsEnabled = _current != null;
        if (_lastSymbolFilter == query && _symbolList.ItemsSource != null) return;
        _lastSymbolFilter = query;
        _symbolList.ItemsSource = MathSymbols.All.Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || s.Glyph.Contains(query, StringComparison.Ordinal)).Select(s => new SymbolRow(s.Glyph, s.Name)).ToArray();
        _symbolList.IsEnabled = _current != null;
    }
    private async Task RefreshSidebarAsync()
    {
        _sidebarWork.Cancel(); _sidebarWork.Dispose(); _sidebarWork = new(); var token = _sidebarWork.Token;
        var mode = SidebarPicker?.SelectedIndex ?? 0; var root = _root;
        try
        {
            if (mode == 1)
            {
                var query = _projectQuery.Text;
                var key = (root, query, _matchCase.IsChecked == true, _revision);
                if (_searchKey == key && _searchResults.ItemsSource != null) return;
                _searchResults.ItemsSource = null;
                if (root == null || string.IsNullOrWhiteSpace(query)) { _searchStatus.Text = T("搜索工程中的 Typst 源码。", "Search the project's Typst source files."); return; }
                _searchStatus.Text = T("正在搜索…", "Searching…"); var matchCase = _matchCase.IsChecked == true;
                var buffers = _buffers.ToDictionary(p => p.Key, p => p.Value.Text, StringComparer.OrdinalIgnoreCase);
                await Task.Delay(200, token);
                var hits = await Task.Run(async () => await ProjectSearch.FindAsync(ProjectFiles.Sources(root, token), buffers, query, matchCase, token), token);
                if (token.IsCancellationRequested || root != _root) return;
                _searchKey = key;
                _searchResults.ItemsSource = hits.Select(h => new SidebarRow($"{Path.GetRelativePath(root, h.Path)}:{h.Line + 1}", h.Preview, h)).ToArray();
                _searchStatus.Text = hits.Count == 200 ? T("显示前 200 个结果", "First 200 results") : T($"{hits.Count} 个结果", $"{hits.Count} results");
            }
            else if (mode == 2)
            {
                if (_outlineTree.RootNodes.Count > 0 && ReferenceEquals(_outlinePdf, _pdf) && (_pdf != null || _outlineSource == _current?.Text)) return;
                var headings = await Preview.GetOutlineAsync(); if (token.IsCancellationRequested) return;
                _outlinePdf = _pdf; _outlineSource = _current?.Text;
                _outlineTree.RootNodes.Clear();
                TreeViewNode Node(PdfHeading heading) { var node = new TreeViewNode { Content = new OutlineRow(heading.Title, heading.Destination), IsExpanded = true }; foreach (var child in heading.Children) node.Children.Add(Node(child)); return node; }
                foreach (var heading in headings) _outlineTree.RootNodes.Add(Node(heading));
                _outlineStatus.Text = headings.Count > 0 ? (_pdfRevision == _revision ? T("主文档大纲", "Main document outline") : T("上次成功编译的大纲", "Last successful document outline")) : T("当前源码标题", "Current source headings");
                if (headings.Count == 0 && _current != null)
                {
                    var parents = new Stack<(int Level, TreeViewNode Node)>();
                    foreach (var span in TypstEditing.Highlight(_current.Text).Where(s => s.Style == 7))
                    {
                        var text = _current.Text.Substring(span.Start, span.Length); var level = text.TakeWhile(c => c == '=').Count();
                        var node = new TreeViewNode { Content = new OutlineRow(text.TrimStart('=').Trim(), null, span.Start), IsExpanded = true };
                        while (parents.Count > 0 && parents.Peek().Level >= level) parents.Pop();
                        if (parents.Count == 0) _outlineTree.RootNodes.Add(node); else parents.Peek().Node.Children.Add(node); parents.Push((level, node));
                    }
                    if (_outlineTree.RootNodes.Count == 0) _outlineStatus.Text = T("暂无标题", "No headings yet");
                }
            }
            else if (mode == 3) FilterSymbols();
            else if (mode == 4)
            {
                var problems = _session?.Problems ?? [];
                var signature = _diagnostics + _compileError;
                _retryCompile.IsEnabled = _session != null && !Busy.IsActive;
                if (_problemSignature == signature && _problemList.ItemsSource != null) return;
                _problemSignature = signature;
                _problemList.ItemsSource = problems.Select(p => new SidebarRow($"{(root != null && Path.IsPathFullyQualified(p.Path) && ProjectFiles.Inside(root, p.Path) ? Path.GetRelativePath(root, p.Path) : p.Path)}:{p.Line + 1}", p.Message, p)).ToArray();
                // Export errors can arrive before LSP diagnostics; never show an empty error list.
                if (problems.Count == 0 && _compileError.Length > 0)
                    _problemList.ItemsSource = new[] { new SidebarRow(T("编译失败", "Compilation failed"), _compileError, _compileError) };
                _problemStatus.Text = problems.Count > 0 ? T($"{problems.Count} 个问题", $"{problems.Count} problems") : _compileError.Length > 0 ? _compileError : T("✓ 没有问题。", "✓ No problems."); _retryCompile.IsEnabled = _session != null && !Busy.IsActive;
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception error) { if (!token.IsCancellationRequested) SetStatus(error.Message); }
    }
}
