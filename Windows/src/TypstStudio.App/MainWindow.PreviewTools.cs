using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using static TypstStudio.App.UiText;
namespace TypstStudio.App;
public sealed partial class MainWindow
{
    private readonly ComboBox _zoomInput = new() { IsEditable = true };
    private readonly List<Button> _expandedPdfButtons = [];
    private bool _updatingZoom;
    private bool _previewEventsInitialized;
    private void InitializePdfToolbar()
    {
        _updatingZoom = true;
        PdfLeftActions.Children.Clear(); PdfRightActions.Children.Clear(); _expandedPdfButtons.Clear();
        _zoomInput.Width = _zoomInput.MinWidth = Ui.D("ScaleBoxWidth"); _zoomInput.Padding = Ui.Pad("Pad4"); _zoomInput.Height = Ui.D("ControlHeightStandard"); _zoomInput.FontSize = Ui.D("FontCaption");
        _zoomInput.Resources["ComboBoxEditableTextPadding"] = Ui.Pad("ZoomTextPadding");
        _zoomInput.ItemsSource = new[] { "50%", "75%", "100%", "125%", "150%", "200%", T("适合宽度", "Fit width"), T("整页", "Fit page") };
        Ui.Describe(_zoomInput, T("PDF 缩放比例", "PDF zoom"));
        PdfLeftActions.Children.Add(Ui.IconButton("\uE71F", T("缩小", "Zoom out"), ZoomOut_Click)); PdfLeftActions.Children.Add(_zoomInput);
        PdfLeftActions.Children.Add(Ui.IconButton("\uE8A3", T("放大", "Zoom in"), ZoomIn_Click));
        void Expanded(StackPanel panel, string glyph, string title, RoutedEventHandler action) { var b = Ui.IconButton(glyph, title, action); _expandedPdfButtons.Add(b); panel.Children.Add(b); }
        Expanded(PdfLeftActions, "\uE7FD", T("适合宽度", "Fit width"), Fit_Click); Expanded(PdfLeftActions, "\uE9A6", T("整页", "Fit page"), FitPage_Click);
        Expanded(PdfRightActions, "\uE70E", T("上一页", "Previous page"), PreviousPage_Click); Expanded(PdfRightActions, "\uE70D", T("下一页", "Next page"), NextPage_Click);
        Expanded(PdfRightActions, "\uE72B", T("定位到源码 (Ctrl+Alt+J)", "Go to source (Ctrl+Alt+J)"), (_, _) => NavigatePreviewSelection());
        PdfRightActions.Children.Add(Ui.IconButton("\uE712", T("更多 PDF 操作", "More PDF actions"), PdfMore_Click));
        _updatingZoom = false;
        if (_previewEventsInitialized) { UpdatePdfToolbar(); return; } _previewEventsInitialized = true;
        _zoomInput.SelectionChanged += (_, _) => { if (!_updatingZoom) ApplyZoomInput(); };
        _zoomInput.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter) { ApplyZoomInput(); e.Handled = true; } };
        PdfToolbar.SizeChanged += (_, _) => { foreach (var button in _expandedPdfButtons) button.Visibility = PdfToolbar.ActualWidth >= Ui.D("PdfExpandedToolbarWidth") ? Visibility.Visible : Visibility.Collapsed; };
        Preview.ViewMetricsChanged += UpdatePdfToolbar;
        Ui.Describe(PageButton, T("跳转页码", "Go to page")); Ui.Describe(Preview, T("Ctrl+单击 PDF 可跳转到源码", "Ctrl+click PDF to locate source"));
    }
    private void ApplyZoomInput()
    {
        if (_updatingZoom) return; var text = _zoomInput.Text.Trim();
        if (text == T("适合宽度", "Fit width")) Preview.FitWidth(); else if (text == T("整页", "Fit page")) Preview.FitPage();
        else if (double.TryParse(text.TrimEnd('%'), out var percent) && double.IsFinite(percent) && percent >= 50 && percent <= 200) Preview.SetZoomPercent(percent);
        else { SetStatus(T("请输入 50%–200% 的缩放比例。", "Enter a zoom between 50% and 200%.")); UpdatePdfToolbar(); }
    }
    private void UpdatePdfToolbar()
    {
        _updatingZoom = true; try { _zoomInput.Text = $"{Preview.ZoomPercent:F0}%"; } finally { _updatingZoom = false; }
        PageLabel.Text = T($"第 {Preview.CurrentPage} / {Preview.PageCount} 页", $"{Preview.CurrentPage} / {Preview.PageCount}");
    }
    private async void PageButton_Click(object sender, RoutedEventArgs e)
    {
        if (Preview.PageCount == 0) return;
        var box = new NumberBox { Minimum = 1, Maximum = Preview.PageCount, Value = Math.Max(1, Preview.CurrentPage), SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        if (await ShowDialogAsync(new ContentDialog { XamlRoot = Root.XamlRoot, Title = T("跳转到页", "Go to page"), Content = box, PrimaryButtonText = T("跳转", "Go"), CloseButtonText = T("取消", "Cancel") }) == ContentDialogResult.Primary && double.IsFinite(box.Value) && box.Value >= 1 && box.Value <= Preview.PageCount) Preview.MovePage((int)box.Value - Preview.CurrentPage);
    }
    private void NavigatePreviewSelection() { if (!Preview.NavigateSelection()) SetStatus(T("先选择 PDF 文字，或 Ctrl+单击公式。", "Select PDF text or Ctrl+click a formula.")); }
    private void PdfMore_Click(object sender, RoutedEventArgs e)
    {
        var menu = new MenuFlyout(); void Add(string title, RoutedEventHandler action) { var item = new MenuFlyoutItem { Text = title }; item.Click += action; menu.Items.Add(item); }
        Add(T("适合宽度", "Fit width"), Fit_Click); Add(T("整页", "Fit page"), FitPage_Click); Add(T("上一页", "Previous page"), PreviousPage_Click); Add(T("下一页", "Next page"), NextPage_Click); Add(T("定位到源码", "Go to source"), (_, _) => NavigatePreviewSelection());
        menu.Items.Add(new MenuFlyoutSeparator()); Add(T("导出 PDF…", "Export PDF…"), Export_Click); Add(T("在外部打开", "Open externally"), ExternalPdf_Click); menu.ShowAt((FrameworkElement)sender);
    }
    private async void ExternalPdf_Click(object sender, RoutedEventArgs e) => await SafeAsync(async () =>
    {
        if (_pdf == null || _pdfRevision != _revision) { SetStatus(T("请先成功编译当前内容。", "Compile the current content successfully first.")); return; }
        var data = _pdf; var folder = Path.Combine(Path.GetTempPath(), "TypstStudio-ExternalPreview"); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "preview-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data))[..16] + ".pdf");
        await File.WriteAllBytesAsync(path, data); await Windows.System.Launcher.LaunchFileAsync(await Windows.Storage.StorageFile.GetFileFromPathAsync(path));
        _ = Task.Run(() => { foreach (var file in Directory.EnumerateFiles(folder, "preview-*.pdf")) { try { if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-7)) File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { } } });
    });
    private void StatusButton_Click(object sender, RoutedEventArgs e) { if (_compileError.Length > 0 || (_session?.Problems.Count ?? 0) > 0) Problems_Click(sender, e); }
    private void UpdateCompilePresentation()
    {
        CompileButton.IsEnabled = _current != null && !_opening && !Busy.IsActive;
        CompileLabel.Text = T("编译", "Compile");
        Ui.Describe(CompileButton, T("编译当前文件", "Compile current file") + ": " + (_current == null ? "—" : Path.GetFileName(_current.Path)) + " (Ctrl+B)\n" + T("预览入口", "Preview entry") + ": " + (_pdfEntry == null ? "—" : Path.GetFileName(_pdfEntry)));

        var count = Math.Max(_session?.Problems.Count ?? 0, _compileError.Length > 0 ? 1 : 0); SidebarPicker.SetProblemCount(count);
        var failed = _compileError.Length > 0 || _session?.Problems.Any(p => p.Severity == 1) == true;
        CompileStateIcon.Glyph = failed ? "\uE783" : "\uE73E"; CompileStateIcon.Visibility = Busy.IsActive || _pdf == null && !failed ? Visibility.Collapsed : Visibility.Visible;
        CompileStateIcon.Foreground = Ui.Brush(Root, failed ? "SystemFillColorCriticalBrush" : "SystemFillColorSuccessBrush");
    }
}
