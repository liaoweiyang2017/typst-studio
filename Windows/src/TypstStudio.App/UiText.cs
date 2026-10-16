using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace TypstStudio.App;
internal static class UiText
{
    public static bool English { get; set; }
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["文件"]="File", ["编辑"]="Edit", ["视图"]="View", ["排版"]="Typeset", ["工具"]="Tools", ["开始页"]="Welcome", ["返回文档"]="Back to document",
        ["打开文件夹…"]="Open Folder…", ["打开文件…"]="Open File…", ["从模板新建…"]="New from Template…", ["从模板新建工程…"]="New Project from Template…",
        ["保存"]="Save", ["保存副本…"]="Save Copy…", ["导出 PDF…"]="Export PDF…", ["查找…"]="Find…", ["查找"]="Find", ["放大 PDF"]="Zoom In", ["缩小 PDF"]="Zoom Out",
        ["适合宽度"]="Fit width", ["编译"]="Compile", ["定位到 PDF"]="Go to PDF", ["定位到 PDF →"]="Go to PDF →", ["编译问题"]="Build Problems", ["设置…"]="Settings…",
        ["设置"]="Settings", ["最近工程"]="Recent Projects", ["暂无最近工程"]="No recent projects", ["打开文件时，其所在文件夹将作为工程。"]="Opening a file uses its folder as the project.",
        ["工程文件"]="Project", ["筛选文件"]="Filter files", ["尚未打开文档"]="No document open", ["问题"]="Problems", ["无问题"]="No problems", ["查看问题"]="View problems",
        ["撤销"]="Undo", ["重做"]="Redo", ["切换行注释"]="Toggle Line Comment", ["重置分栏"]="Reset split", ["上一页"]="Previous page", ["下一页"]="Next page", ["整页"]="Fit page",
        ["关闭"]="Close", ["取消"]="Cancel", ["全部保存"]="Save All", ["放弃修改"]="Discard", ["保存未保存的文档？"]="Save unsaved documents?", ["没有编译问题。"]="No build problems.",
        ["选择保存位置"]="Choose location", ["从模板新建"]="New from Template", ["查找当前源码"]="Find in source", ["打开工程文件夹，开始编写 Typst 文档。"]="Open a project folder to start writing Typst."
    };
    public static string T(string chinese, string english) => English ? english : chinese;
    public static string Translate(string value)
    { if (English) return Labels.GetValueOrDefault(value, value); foreach (var pair in Labels) if (pair.Value == value) return pair.Key; return value; }
    public static void Apply(DependencyObject root)
    {
        if (root is SourceEditor or TreeView) return;
        if (root is TextBlock text) text.Text = Translate(text.Text);
        if (root is Button button && button.Content is string content) button.Content = Translate(content);
        if (root is AutoSuggestBox search) search.PlaceholderText = Translate(search.PlaceholderText);
        if (root is MenuBar menu) foreach (var item in menu.Items) { item.Title = Translate(item.Title); foreach (var child in item.Items.OfType<MenuFlyoutItem>()) child.Text = Translate(child.Text); }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) Apply(VisualTreeHelper.GetChild(root, i));
    }
}
