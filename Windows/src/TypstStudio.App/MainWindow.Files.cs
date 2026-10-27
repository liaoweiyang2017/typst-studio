using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace TypstStudio.App;
public sealed partial class MainWindow
{
    private int _treeEpoch;
    [Microsoft.UI.Xaml.Data.Bindable]
    public sealed record FileItem(string Name, string? Path, bool IsFolder = false) : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _expanded, _entry;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public Microsoft.UI.Xaml.Media.Imaging.SvgImageSource IconSource => FileIcons.For(Path!, IsFolder, _expanded);
        public Windows.UI.Text.FontWeight EntryWeight => _entry ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        public bool IsExpanded { get => _expanded; set { _expanded = value; PropertyChanged?.Invoke(this, new(nameof(IconSource))); } }
        public void SetEntry(string? entry) { _entry = !IsFolder && string.Equals(Path, entry, StringComparison.OrdinalIgnoreCase); PropertyChanged?.Invoke(this, new(nameof(EntryWeight))); }
        public override string ToString() => Name;
    }
    private static FileItem[] ReadDirectory(string root) => Directory.EnumerateFileSystemEntries(root)
        .Where(p => !System.IO.Path.GetFileName(p).StartsWith('.') && !File.GetAttributes(p).HasFlag(FileAttributes.ReparsePoint))
        .Select(p => new FileItem(System.IO.Path.GetFileName(p), p, Directory.Exists(p))).OrderByDescending(p => p.IsFolder).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    private async void BuildTree()
    {
        if (Files == null || _root == null) return; var root = _root; var epoch = ++_treeEpoch; var filter = FileFilter.Text ?? "";
        try
        {
            var entries = await Task.Run(() => filter.Length == 0 ? ReadDirectory(root) : FindEntries(root, filter));
            if (root != _root || epoch != _treeEpoch) return; Files.RootNodes.Clear();
            foreach (var item in entries)
            {
                item.SetEntry(_entry);
                var node = new TreeViewNode { Content = item, HasUnrealizedChildren = item.IsFolder }; Files.RootNodes.Add(node);
                if (item.IsFolder && _entry != null && TypstStudio.Core.ProjectFiles.Inside(item.Path!, _entry)) { await PopulateFolderAsync(node, root, epoch); node.IsExpanded = true; }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { SetStatus(error.Message); }
    }
    private static FileItem[] FindEntries(string root, string filter)
    {
        var found = new List<FileItem>(); var folders = new Stack<string>(); folders.Push(root);
        while (folders.TryPop(out var folder) && found.Count < 1000)
        {
            try { foreach (var item in ReadDirectory(folder)) { if (item.Name is "node_modules" or "bin" or "obj") continue; if (item.IsFolder) folders.Push(item.Path!); if (item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) found.Add(item with { Name = System.IO.Path.GetRelativePath(root, item.Path!) }); } }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        return found.ToArray();
    }
    private async Task PopulateFolderAsync(TreeViewNode node, string root, int epoch)
    {
        if (!node.HasUnrealizedChildren || node.Content is not FileItem { IsFolder: true, Path: not null } folder) return;
        node.HasUnrealizedChildren = false;
        try
        {
            var entries = await Task.Run(() => ReadDirectory(folder.Path)); if (root != _root || epoch != _treeEpoch) return;
            foreach (var item in entries)
            {
                item.SetEntry(_entry);
                var child = new TreeViewNode { Content = item, HasUnrealizedChildren = item.IsFolder }; node.Children.Add(child);
                if (item.IsFolder && _entry != null && TypstStudio.Core.ProjectFiles.Inside(item.Path!, _entry)) { await PopulateFolderAsync(child, root, epoch); child.IsExpanded = true; }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { SetStatus(error.Message); }
    }
    private async void Files_Expanding(TreeView sender, TreeViewExpandingEventArgs e) { ((FileItem)e.Node.Content).IsExpanded = true; if (_root != null) await PopulateFolderAsync(e.Node, _root, _treeEpoch); }
    private void Files_Collapsed(TreeView sender, TreeViewCollapsedEventArgs e) => ((FileItem)e.Node.Content).IsExpanded = false;
    private void UpdateTreeEntry()
    {
        void Visit(IEnumerable<TreeViewNode> nodes) { foreach (var node in nodes) { ((FileItem)node.Content).SetEntry(_entry); Visit(node.Children); } }
        Visit(Files.RootNodes);
    }
    private async void Files_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        var item = args.InvokedItem is TreeViewNode node ? node.Content as FileItem : args.InvokedItem as FileItem;
        if (item?.Path == null || item.IsFolder || _opening) return;
        await SafeAsync(async () => { if (System.IO.Path.GetExtension(item.Path).Equals(".typ", StringComparison.OrdinalIgnoreCase)) await OpenBufferAsync(item.Path); else if (new[] { ".json", ".md", ".txt", ".bib", ".yaml", ".yml", ".toml", ".csv", ".rst", ".tex", ".py", ".sh", ".bat", ".ps1" }.Contains(System.IO.Path.GetExtension(item.Path), StringComparer.OrdinalIgnoreCase))
            { if (new FileInfo(item.Path).Length > 1_000_000) throw new IOException(UiText.T("该文件过大，请在文件管理器中打开。", "This file is too large for a read-only preview.")); await ShowMessageAsync(item.Name, await File.ReadAllTextAsync(item.Path)); }
            else SetStatus(UiText.T("此类型请在文件管理器中打开。", "Open this file type from your file manager.")); });
    }
}
