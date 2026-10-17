using Microsoft.UI.Xaml;
namespace TypstStudio.App;
public sealed partial class MainWindow
{
    private async Task VerifyCompileTargetAsync()
    {
        var main = _entry!; var child = _current!.Path; var session = _session; var pdf = _pdf; var version = _revision;
        await OpenBufferAsync(main); await OpenBufferAsync(child); await Task.Delay(_compileDelay + 100);
        if (_entry != main || _revision != version || !ReferenceEquals(_pdf, pdf) || Busy.IsActive) throw new InvalidOperationException("Browsing must not compile or replace the PDF");
        await CompileCurrentAsync();
        if (_entry != child || _session != session || _pdfRevision != _revision) throw new InvalidOperationException("Explicit command did not compile the current editor in the existing session");
        var success = _pdf; var original = Editor.Text;
        _autoCompile = false; SelectSource(original.Length, 0); Editor.SelectedText = "\n#set text(font: )";
        await CompileCurrentAsync();
        if (_pdfRevision == _revision || !ReferenceEquals(success, _pdf)) throw new InvalidOperationException("Failed explicit compile must retain but invalidate the last PDF");
        Editor.Undo(); await CompileCurrentAsync(); _autoCompile = true;
        await OpenBufferAsync(main); await CompileCurrentAsync(); await OpenBufferAsync(child);
        var first = CompileCurrentAsync(); await OpenBufferAsync(main); var last = CompileCurrentAsync();
        await Task.WhenAll(first, last); await OpenBufferAsync(child);
        if (_entry != main || _session != session || _pdfRevision != _revision) throw new InvalidOperationException("Main target was not restored");
    }
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
