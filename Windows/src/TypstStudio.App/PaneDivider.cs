using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
namespace TypstStudio.App;
public sealed class PaneDivider : UserControl
{
    public event DragDeltaEventHandler? DragDelta;
    public event DragCompletedEventHandler? DragCompleted;
    public PaneDivider()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        var thumb = new Thumb { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        thumb.Template = (ControlTemplate)XamlReader.Load("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Grid Background='Transparent'><Border Width='1' Background='{ThemeResource DividerStrokeColorDefaultBrush}'/></Grid></ControlTemplate>");
        thumb.DragDelta += (_, args) => DragDelta?.Invoke(this, args);
        thumb.DragCompleted += (_, args) => DragCompleted?.Invoke(this, args);
        Content = thumb;
    }
}
