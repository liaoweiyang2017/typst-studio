using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace TypstStudio.App;
public sealed class SidebarTabs : UserControl
{
    private readonly SelectorBar _bar = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly InfoBadge _badge = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    public IList<SelectorBarItem> Items => _bar.Items;
    public event EventHandler? SelectionChanged;
    public int SelectedIndex { get => _bar.SelectedItem == null ? -1 : _bar.Items.IndexOf(_bar.SelectedItem); set { if (value >= 0 && value < Items.Count) _bar.SelectedItem = Items[value]; } }
    public SidebarTabs()
    {
        var grid = new Grid(); grid.Children.Add(_bar); grid.Children.Add(_badge); Content = grid;
        _bar.SizeChanged += (_, _) => PositionBadge(grid);
        _bar.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    public void SetItems(string[] labels, string[] glyphs)
    {
        if (Items.Count == 0) foreach (var _ in labels) Items.Add(new SelectorBarItem { Width = Ui.D("ControlHeightStandard"), MinWidth = Ui.D("ControlHeightStandard"), Height = Ui.D("ControlHeightStandard"), Padding = Ui.Pad("Pad4"), Margin = Ui.Pad("Zero") });
        for (var i = 0; i < Items.Count; i++) { Items[i].Icon = Ui.Icon(glyphs[i]); Items[i].Text = ""; Ui.Describe(Items[i], labels[i]); }
    }
    public void SetProblemCount(int count) { _badge.Value = count; _badge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed; }
    private void PositionBadge(Grid host)
    {
        var item = Items[4]; var point = item.TransformToVisual(host).TransformPoint(new Windows.Foundation.Point());
        _badge.Margin = new Thickness(point.X + item.ActualWidth - Ui.D("FontIcon"), point.Y, 0, 0);
    }
}
