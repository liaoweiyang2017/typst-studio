using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace TypstStudio.App;
internal static class Ui
{
    public static double D(string key) => Convert.ToDouble(Application.Current.Resources[key]);
    public static Thickness Pad(string key) => (Thickness)Application.Current.Resources[key];
    public static Brush Brush(FrameworkElement element, string key)
    {
        var resource = (Border)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            $"<Border xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' RequestedTheme='{element.ActualTheme}' Background='{{ThemeResource {key}}}'/>");
        return resource.Background;
    }

    public static Windows.UI.Color OpaqueColor(FrameworkElement element, string key, Windows.UI.Color? background = null)
    {
        var bg = background ?? ((SolidColorBrush)Brush(element, "SolidBackgroundFillColorBaseBrush")).Color;
        var c = ((SolidColorBrush)Brush(element, key)).Color; var a = c.A / 255.0;
        return Windows.UI.Color.FromArgb(255, (byte)(c.R * a + bg.R * (1 - a)), (byte)(c.G * a + bg.G * (1 - a)), (byte)(c.B * a + bg.B * (1 - a)));
    }
    public static Style Style(string key) => (Style)Application.Current.Resources[key];
    public static CornerRadius Radius(string key) => (CornerRadius)Application.Current.Resources[key];
    public static FontIcon Icon(string glyph) => new() { Glyph = glyph, FontFamily = (FontFamily)Application.Current.Resources["StudioIconFont"], FontSize = D("FontIcon") };
    public static void Describe(FrameworkElement element, string text)
    { ToolTipService.SetToolTip(element, text); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(element, text); }
    public static Button IconButton(string glyph, string label, RoutedEventHandler action)
    { var button = new Button { Content = Icon(glyph), Style = Style("StudioIconButton") }; Describe(button, label); button.Click += action; return button; }
}
