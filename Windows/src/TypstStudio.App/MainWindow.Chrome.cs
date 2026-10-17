using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Foundation;
namespace TypstStudio.App;
public sealed partial class MainWindow
{
    private bool _sidebarHidden;
    private double _sidebarSavedWidth;
    private void ConfigureChrome()
    {
        if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported()) SystemBackdrop = new MicaBackdrop();
        else Root.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"];
        ExtendsContentIntoTitleBar = true; SetTitleBar(StudioTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        Root.SizeChanged += (_, _) => UpdateWelcomeLayout();
        StudioTitleBar.SizeChanged += (_, _) => UpdateTitleBarRegions();
        TitleLeft.SizeChanged += (_, _) => UpdateTitleBarRegions();
        MainMenu.SizeChanged += (_, _) => UpdateTitleBarRegions();
        AppWindow.Changed += (_, _) => UpdateTitleBarRegions();
        Root.Loaded += (_, _) => { Root.XamlRoot.Changed += (_, _) => UpdateTitleBarRegions(); UpdateTitleBarRegions(); SyncCaptionColors(); };
        Ui.Describe(RestoreSidebarButton, UiText.T("显示侧边栏 (Ctrl+Shift+B)", "Show sidebar (Ctrl+Shift+B)"));
    }
    private void UpdateTitleBarRegions()
    {
        if (Root.XamlRoot == null || MainMenu.ActualWidth == 0) return;
        var scale = Root.XamlRoot.RasterizationScale;
        CaptionInsetColumn.Width = new GridLength(AppWindow.TitleBar.RightInset / scale);
        RectInt32 Region(FrameworkElement element)
        {
            var r = element.TransformToVisual(Root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            return new((int)Math.Floor(r.X * scale), (int)Math.Floor(r.Y * scale), (int)Math.Ceiling(r.Width * scale), (int)Math.Ceiling(r.Height * scale));
        }
        var areas = new List<RectInt32> { Region(MainMenu) }; if (RestoreSidebarButton.Visibility == Visibility.Visible) areas.Add(Region(RestoreSidebarButton));
        InputNonClientPointerSource.GetForWindowId(AppWindow.Id).SetRegionRects(NonClientRegionKind.Passthrough, areas.ToArray());
        var menuEnd = MainMenu.TransformToVisual(Root).TransformPoint(new Point(MainMenu.ActualWidth, 0)).X;
        TitleFileName.MaxWidth = Math.Max(0, Math.Min(Ui.D("TitleMaxWidth"), Root.ActualWidth - 2 * (menuEnd + Ui.D("Space12"))));
    }
    private void SyncCaptionColors()
    {
        if (!ExtendsContentIntoTitleBar) return;
        if (SystemBackdrop == null) Root.Background = Ui.Brush(Root, "SolidBackgroundFillColorBaseBrush");
        if (TitleFileName.Foreground is SolidColorBrush foreground) AppWindow.TitleBar.ButtonForegroundColor = foreground.Color;
        if (Root.Background is SolidColorBrush background) AppWindow.TitleBar.ButtonBackgroundColor = AppWindow.TitleBar.ButtonInactiveBackgroundColor = background.Color;
    }
    private void SyncChrome()
    {
        TitleFileName.Visibility = Workspace.Visibility == Visibility.Visible && _current != null ? Visibility.Visible : Visibility.Collapsed;
        RestoreSidebarButton.Visibility = Workspace.Visibility == Visibility.Visible && _sidebarHidden ? Visibility.Visible : Visibility.Collapsed;
        StatusArea.BorderThickness = Workspace.Visibility == Visibility.Visible ? Ui.Pad("ThinTop") : Ui.Pad("Zero");
        UpdateTitleBarRegions();
    }
    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        _sidebarHidden = !_sidebarHidden;
        if (_sidebarHidden) { _sidebarSavedWidth = SidebarColumn.ActualWidth; SidebarColumn.MinWidth = 0; SidebarColumn.Width = new GridLength(0); SidebarDivider.Visibility = Visibility.Collapsed; }
        else { SidebarColumn.MinWidth = Ui.D("SidebarMinWidth"); SidebarColumn.Width = new GridLength(Math.Clamp(_sidebarSavedWidth, Ui.D("SidebarMinWidth"), Ui.D("SidebarMaxWidth"))); SidebarDivider.Visibility = Visibility.Visible; }
        SidebarContainer.Visibility = _sidebarHidden ? Visibility.Collapsed : Visibility.Visible; SidebarDividerColumn.Width = new GridLength(_sidebarHidden ? 0 : Ui.D("DividerHitWidth"));
        SyncChrome();
    }
}
