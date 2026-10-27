using Microsoft.UI.Xaml;
namespace TypstStudio.App;
public sealed partial class MainWindow
{
    private async Task CaptureProductAsync(string root, string output)
    {
        Directory.CreateDirectory(output);
        var dark = _args.Contains("--dark-green");
        try {
            _language = "en"; _theme = dark ? "Dark" : "Light"; Root.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light; ApplyLanguage(); UpdateCaption();
            _recent.Clear(); _recent.Add(root); RefreshRecent(); Home_Click(this, new RoutedEventArgs());
            var scale = Root.XamlRoot.RasterizationScale; AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(1200 * scale), (int)(800 * scale))); await Task.Delay(200);
            await CaptureAsync(Path.Combine(output, "windows-welcome.png"));
            await OpenProjectAsync(root, confirmed: true); if (_watcher != null) _watcher.EnableRaisingEvents = false;
            foreach (var node in Files.RootNodes.Where(n => ((FileItem)n.Content).IsFolder).Take(1)) node.IsExpanded = true;
            Files.SelectedNode = Files.RootNodes.First(n => string.Equals(((FileItem)n.Content).Path, _entry, StringComparison.OrdinalIgnoreCase));
            _pdfMode = dark ? "Green" : "Standard"; Preview.SetColorMode(_pdfMode); UpdateCaption(); await Task.Delay(350); Editor.ApplyStyle(); UpdateEditorPosition(); Preview.FitPage(); await Preview.WaitForRenderAsync();
            await CaptureAsync(Path.Combine(output, dark ? "windows-editor-dark-green.png" : "windows-editor-light.png"));
            _theme = "Light"; Root.RequestedTheme = ElementTheme.Light; UpdateCaption(); Settings_Click(this, new RoutedEventArgs()); await Task.Delay(200);
            await CaptureAsync(Path.Combine(output, "windows-settings.png"));
            await File.WriteAllTextAsync(Path.Combine(output, "windows-capture.txt"), "ok");
        } catch (Exception error) { await File.WriteAllTextAsync(Path.Combine(output, "windows-capture-error.txt"), error.ToString()); }
        _closing = true; await DisposeProjectAsync(); Close();
    }
    private async Task VerifyFluentAsync(string root)
    {
        Root.UpdateLayout();
        if (_pdfMode != "Standard" || _zoomInput.ActualWidth < 96) throw new InvalidOperationException("Fresh PDF defaults or zoom width failed");
        if (Math.Abs(Editor.LineHeight - Editor.FontSize * 1.5) > 1) throw new InvalidOperationException("Native code line height is not 1.5×");
        if (CountWords("") != 0 || CountWords("one two\nthree") != 3 || CountWords("中文 alpha 𠀀") != 4) throw new InvalidOperationException("Word counting failed");
        if (!ReferenceEquals(CompileButton.Style, Ui.Style("AccentButtonStyle")) || FormatToolbar.Children.Any(c => c is not Microsoft.UI.Xaml.Controls.AppBarButton)) throw new InvalidOperationException("Command styles failed");
        var cases = new[] { ("MAIN.TYP", "typ"), ("main.pdf", "pdf"), ("data.json", "json"), ("README.md", "markdown"), ("x.png", "image"), ("x.jpg", "image"), ("x.svg", "image"), ("x.bat", "terminal"), ("x.cmd", "terminal"), ("x.ps1", "terminal"), ("x.txt", "document"), ("x.unknown", "document") };
        var sources = new HashSet<Microsoft.UI.Xaml.Media.Imaging.SvgImageSource>();
        foreach (var (file, icon) in cases) {
            var source = FileIcons.For(file, false, false);
            if (!source.UriSource.AbsolutePath.EndsWith("/" + icon + ".svg")) throw new InvalidOperationException("Icon mapping failed: " + file);
            sources.Add(source);
        }
        var folder = new FileItem("assets", Path.Combine(root, "assets"), true);
        var closed = folder.IconSource; folder.IsExpanded = true;
        if (ReferenceEquals(closed, folder.IconSource)) throw new InvalidOperationException("Expanded folder did not change icon");
        sources.Add(closed); sources.Add(folder.IconSource);
        Directory.CreateDirectory(folder.Path!);
        var folderNode = new Microsoft.UI.Xaml.Controls.TreeViewNode { Content = folder, HasUnrealizedChildren = true };
        folder.IsExpanded = false; Files.RootNodes.Add(folderNode); folderNode.IsExpanded = true; Root.UpdateLayout();
        if (!folder.IsExpanded) throw new InvalidOperationException("Tree expansion did not update the folder icon");
        folderNode.IsExpanded = false; Root.UpdateLayout();
        if (folder.IsExpanded) throw new InvalidOperationException("Tree collapse did not update the folder icon");
        Files.RootNodes.Remove(folderNode);
        foreach (var source in sources) {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(Path.Combine(AppContext.BaseDirectory, source.UriSource.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            using var stream = await file.OpenReadAsync();
            var result = await new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource().SetSourceAsync(stream);
            if (result != Microsoft.UI.Xaml.Media.Imaging.SvgImageSourceLoadStatus.Success) throw new InvalidOperationException("SVG decode failed: " + file.Name);
        }
        var originalTheme = Root.RequestedTheme;
        Root.RequestedTheme = ElementTheme.Dark; await Task.Delay(100); Editor.ApplyStyle();
        var darkBackground = Ui.OpaqueColor(Editor, "LayerFillColorDefaultBrush");
        if (darkBackground.R > 128) throw new InvalidOperationException("Dark theme resolved a light editor background");
        Root.RequestedTheme = ElementTheme.Light; await Task.Delay(100); Editor.ApplyStyle();
        if (Ui.OpaqueColor(Editor, "LayerFillColorDefaultBrush").R < 128) throw new InvalidOperationException("Light theme resolved a dark editor background");
        Root.RequestedTheme = originalTheme;
        SidebarPicker.SetProblemCount(13); Root.UpdateLayout();
        var badge = ((Microsoft.UI.Xaml.Controls.Grid)SidebarPicker.Content).Children.OfType<Microsoft.UI.Xaml.Controls.InfoBadge>().Single();
        var item = SidebarPicker.Items[4]; var origin = item.TransformToVisual((Microsoft.UI.Xaml.Controls.Grid)SidebarPicker.Content).TransformPoint(new Windows.Foundation.Point());
        if (badge.Visibility != Visibility.Visible || badge.Value != 13 || badge.Margin.Left < origin.X || badge.Margin.Left > origin.X + item.ActualWidth) throw new InvalidOperationException("Problems badge is not anchored to its item");
        await CaptureAsync(Path.Combine(root, "fluent-icons-badge.png"));
        SidebarPicker.SetProblemCount(0);
        if (badge.Visibility != Visibility.Collapsed) throw new InvalidOperationException("Zero badge did not hide");
        var selected = SidebarPicker.SelectedIndex; _language = "en"; RefreshSidebarLabels();
        if (SidebarPicker.SelectedIndex != selected || SidebarPicker.Items.Any(i => ((Microsoft.UI.Xaml.Controls.FontIcon)i.Icon).FontSize != 16)) throw new InvalidOperationException("Native selector changed selection or icon size");
        _language = "zh"; RefreshSidebarLabels();
    }
    private async Task VerifyCompileTargetAsync()
    {
        var main = _entry!; var child = _current!.Path; var session = _session; var pdf = _pdf; var version = _revision;
        await OpenBufferAsync(main); await OpenBufferAsync(child); await Task.Delay(_compileDelay + 100);
        if (_entry != main || _revision != version || !ReferenceEquals(_pdf, pdf) || Busy.IsActive) throw new InvalidOperationException("Browsing must not compile or replace the PDF");
        await CompileCurrentAsync();
        if (Files.RootNodes.Any(n => ((FileItem)n.Content).EntryWeight.Weight != (string.Equals(((FileItem)n.Content).Path, child, StringComparison.OrdinalIgnoreCase) ? Microsoft.UI.Text.FontWeights.SemiBold.Weight : Microsoft.UI.Text.FontWeights.Normal.Weight))) throw new InvalidOperationException("Tree emphasis did not follow the compile entry");
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
        var editorBottom = EditorToolbar.TransformToVisual(Root).TransformPoint(new Windows.Foundation.Point(0, EditorToolbar.ActualHeight)).Y;
        var previewBottom = PdfToolbar.TransformToVisual(Root).TransformPoint(new Windows.Foundation.Point(0, PdfToolbar.ActualHeight)).Y;
        if (Math.Abs(editorBottom - previewBottom) > 0.5) throw new InvalidOperationException("Editor and PDF toolbar baselines must align");
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
