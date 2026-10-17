using Microsoft.UI.Xaml;
namespace TypstStudio.App;
public sealed partial class MainWindow
{
    private async Task VerifyEditorCommandsAsync()
    {
        Root.UpdateLayout();
        foreach (var element in FormatToolbar.Children.OfType<FrameworkElement>())
        {
            if (element.ActualHeight != Ui.D("FormatButtonSize") || element.ActualHeight > FormatToolbar.ActualHeight) throw new InvalidOperationException("Format button is clipped");
        }
        var original = Editor.Text;
        _autoCompile = false;
        try
        {
            ShowFind(); FindQuery.Text = "alpha"; SelectSource(0, 0);
            if (FindInEditor(true) < 0 || Editor.SelectedText != "alpha") throw new InvalidOperationException("Find failed on Typst source");
            ReplaceQuery.Text = "beta"; ReplaceAll_Click(this, new RoutedEventArgs());
            if (Editor.Text.Contains("alpha") || !Editor.Text.Contains("beta")) throw new InvalidOperationException("Replace all failed");
            Editor.Undo();
            if (Editor.Text != original) throw new InvalidOperationException("Replace all must undo as one edit");
            var index = original.IndexOf("alpha", StringComparison.Ordinal); SelectSource(index, 5);
            WrapSelection("*", "*");
            if (!Editor.Text.Contains("*alpha*")) throw new InvalidOperationException("Format insertion failed");
            Editor.Undo();
            if (Editor.Text != original) throw new InvalidOperationException("Format must undo as one edit");
            CloseFind();
            if (FindOverlay.Visibility != Visibility.Collapsed) throw new InvalidOperationException("Find did not close");
        }
        finally { _autoCompile = true; }
        await CompileNowAsync(); await WaitForCompileAsync(original);
    }
}
