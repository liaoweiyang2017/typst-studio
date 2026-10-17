using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using System.Runtime.InteropServices.WindowsRuntime;
using TypstStudio.Core;
using Windows.Foundation;
using Windows.System;

namespace TypstStudio.App;
public sealed class PdfViewer : Grid
{
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly StackPanel _stack = new() { Spacing = Ui.D("PaperMargin"), Margin = new Thickness(Ui.D("PaperMargin")) };
    private readonly List<(Grid Host, Image Image, Canvas Marker)> _pages = [];
    private readonly Dictionary<int, int> _renderedWidths = [];
    private readonly SemaphoreSlim _renderGate = new(1);
    private PdfDocument? _document;
    private CancellationTokenSource _render = new();
    private double _zoom = 1;
    private double PaperInset => Ui.D("PaperMargin");
    private double AvailablePageWidth => Math.Max(Ui.D("MinimumPaperWidth"), ActualWidth - 2 * PaperInset - Ui.D("Space4"));
    public int PageCount => _pages.Count;
    public double ZoomPercent => _document == null || _pages.Count == 0 ? 100 : _pages[0].Host.Width / _document.Pages[0].Width * 72 / 96 * 100;
    public event Action? ViewMetricsChanged;
    public void SetZoomPercent(double percent) { if (_document == null || _pages.Count == 0) return; _fitPage = false; _zoom = Math.Clamp(percent, 50, 200) / 100 * 96 / 72 * _document.Pages[0].Width / AvailablePageWidth; LayoutPages(); ScheduleRender(); }

    private long _generation;
    private (int Page, Point Point)? _selectionStart;
    private PdfTextSelection? _selection;
    private int _selectionPage;
    public event Action<int, double, double>? SourceRequested;
    public event Action<string>? Error;
    public event Action<int, int>? PageChanged;
    private string _colorMode = "Standard";
    private bool _fitPage;
    public int CurrentPage
    {
        get { double y = PaperInset; for (var i = 0; i < _pages.Count; i++) { y += _pages[i].Host.Height + PaperInset; if (y > _scroll.VerticalOffset + Ui.D("EditorHeaderHeight")) return i + 1; } return _pages.Count; }
    }
    public void MovePage(int delta)
    { if (_pages.Count == 0) return; var index = Math.Clamp(CurrentPage - 1 + delta, 0, _pages.Count - 1); _scroll.ChangeView(null, _pages.Take(index).Sum(p => p.Host.Height + PaperInset), null, true); }
    public Task<IReadOnlyList<PdfHeading>> GetOutlineAsync() { var document = _document; return document == null ? Task.FromResult<IReadOnlyList<PdfHeading>>([]) : Task.Run(document.ReadOutline); }
    public void FitPage() { _fitPage = true; _zoom = 1; LayoutPages(); ScheduleRender(); }
    public void SetColorMode(string mode)
    {
        if (_colorMode == mode) return; _colorMode = mode; _generation++; _render.Cancel(); _render.Dispose(); _render = new(); _renderedWidths.Clear(); ScheduleRender();
    }
    private static PdfPixels ApplyColor(PdfPixels pixels, string mode)
    {
        if (mode == "Standard") return pixels;
        var data = pixels.Bgra;
        for (var i = 0; i < data.Length; i += 4)
        {
            if (mode == "Night") { data[i] = (byte)(235 - data[i] * 0.78); data[i + 1] = (byte)(235 - data[i + 1] * 0.78); data[i + 2] = (byte)(235 - data[i + 2] * 0.78); }
            else { data[i] = (byte)(data[i] * 0.80); data[i + 1] = (byte)(data[i + 1] * 0.90); data[i + 2] = (byte)(data[i + 2] * 0.79); }
        }
        return pixels;
    }
    public PdfViewer()
    {
        Background = Ui.Brush(this, "SolidBackgroundFillColorBaseBrush"); ActualThemeChanged += (_, _) => Background = Ui.Brush(this, "SolidBackgroundFillColorBaseBrush");
        _scroll.Content = _stack; Children.Add(_scroll);
        _scroll.ViewChanged += (_, _) => { ViewMetricsChanged?.Invoke(); PageChanged?.Invoke(CurrentPage, _pages.Count); ScheduleRender(); };
        SizeChanged += (_, _) => { LayoutPages(); ScheduleRender(); };
    }
    public async Task SetDocumentAsync(byte[] bytes, Func<bool>? isCurrent = null)
    {
        var generation = ++_generation;
        var document = await Task.Run(() => new PdfDocument(bytes));
        if (generation != _generation || isCurrent?.Invoke() == false) { await Task.Run(document.Dispose); return; }
        _render.Cancel(); _render.Dispose(); _render = new();
        var old = _document; _document = document; _selection = null;
        if (old != null) _ = Task.Run(old.Dispose);
        var offset = _scroll.VerticalOffset;
        _stack.Children.Clear(); _pages.Clear(); _renderedWidths.Clear();
        for (var i = 0; i < document.Pages.Count; i++)
        {
            var page = i;
            var image = new Image { Stretch = Stretch.Fill };
            var marker = new Canvas { IsHitTestVisible = false };
            var host = new Grid { IsTabStop = true, Background = Ui.Brush(this, "LayerFillColorDefaultBrush"), HorizontalAlignment = HorizontalAlignment.Center };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(host, $"PDF 第 {page + 1} 页");
            host.Shadow = new ThemeShadow(); host.Translation = new System.Numerics.Vector3(0, 0, (float)Ui.D("PaperElevation"));
            host.Children.Add(image); host.Children.Add(marker);
            var copyKey = new KeyboardAccelerator { Key = VirtualKey.C, Modifiers = VirtualKeyModifiers.Control };
            copyKey.Invoked += (_, args) => { CopySelection(); args.Handled = true; }; host.KeyboardAccelerators.Add(copyKey);
            host.PointerPressed += (_, args) => Press(page, host, args);
            host.PointerReleased += (_, args) => Release(page, host, args);
            host.PointerCanceled += (_, _) => _selectionStart = null;
            var menu = new MenuFlyout(); var source = new MenuFlyoutItem { Text = "在源码中定位 (Ctrl+单击)" };
            source.Click += (_, _) => { if (_selection != null && _selectionPage == page) SourceRequested?.Invoke(page, _selection.Left + _selection.Width / 2, _selection.Top + _selection.Height / 2); };
            var copy = new MenuFlyoutItem { Text = "复制选中文字" }; copy.Click += (_, _) => CopySelection();
            menu.Opening += (_, _) => { source.Text = UiText.T("在源码中定位 (Ctrl+单击)", "Go to source (Ctrl+click)"); copy.Text = UiText.T("复制选中文字", "Copy selected text"); source.IsEnabled = copy.IsEnabled = _selection != null && _selectionPage == page; };
            menu.Items.Add(source); menu.Items.Add(copy); host.ContextFlyout = menu;
            _pages.Add((host, image, marker)); _stack.Children.Add(host);
        }
        PageChanged?.Invoke(CurrentPage, _pages.Count); LayoutPages(); _scroll.UpdateLayout(); _scroll.ChangeView(null, offset, null, true); ScheduleRender();
    }
    public void Zoom(double factor) { if (_fitPage && _pages.Count > 0) _zoom = _pages[0].Host.Width / AvailablePageWidth; _fitPage = false; _zoom = Math.Clamp(_zoom * factor, 0.4, 3); LayoutPages(); ScheduleRender(); }
    public void FitWidth() { _fitPage = false; _zoom = 1; LayoutPages(); ScheduleRender(); }
    private void LayoutPages()
    {
        if (_document == null || ActualWidth < 50) return;
        var width = AvailablePageWidth * _zoom;
        if (_fitPage) width = Math.Min(width, Math.Max(Ui.D("MinimumPaperWidth"), ActualHeight - 2 * PaperInset) * _document.Pages[0].Width / _document.Pages[0].Height);
        for (var i = 0; i < _pages.Count; i++)
        { var size = _document.Pages[i]; _pages[i].Host.Width = width; _pages[i].Host.Height = width * size.Height / size.Width; _pages[i].Marker.Children.Clear(); }
        ViewMetricsChanged?.Invoke();
        _scroll.HorizontalScrollBarVisibility = _zoom > 1 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
    }
    private async void ScheduleRender()
    {
        var document = _document; if (document == null) return;
        var generation = _generation; var token = _render.Token; var mode = _colorMode;
        try
        {
            await _renderGate.WaitAsync(token);
            try
            {
                if (generation != _generation) return;
                var y = PaperInset; var visible = new HashSet<int>();
                for (var i = 0; i < _pages.Count; i++)
                {
                    var item = _pages[i]; var bottom = y + item.Host.Height;
                    if (bottom >= _scroll.VerticalOffset - 250 && y <= _scroll.VerticalOffset + ActualHeight + 250) visible.Add(i);
                    y = bottom + PaperInset;
                }
                foreach (var index in _renderedWidths.Keys.ToArray()) if (!visible.Contains(index)) { _pages[index].Image.Source = null; _renderedWidths.Remove(index); }
                foreach (var index in visible)
                {
                    var width = Math.Clamp((int)Math.Ceiling(_pages[index].Host.Width * (XamlRoot?.RasterizationScale ?? 1)), 100, 4096);
                    if (_renderedWidths.GetValueOrDefault(index) == width) continue;
                    var pixels = await Task.Run(() => ApplyColor(document.Render(index, width, token), mode), token);
                    if (generation != _generation) return;
                    var bitmap = new WriteableBitmap(pixels.Width, pixels.Height);
                    using (var stream = bitmap.PixelBuffer.AsStream()) await stream.WriteAsync(pixels.Bgra, token);
                    bitmap.Invalidate(); _pages[index].Image.Source = bitmap; _renderedWidths[index] = width;
                }
            }
            finally { _renderGate.Release(); }
        }
        catch (OperationCanceledException) { } catch (ObjectDisposedException) { } catch (Exception error) { Error?.Invoke(error.Message); }
    }
    public void GoTo(PdfPoint point)
    {
        var index = point.Page - 1; if (_document == null || index < 0 || index >= _pages.Count) return;
        var scale = _pages[index].Host.Width / _document.Pages[index].Width;
        var y = PaperInset + _pages.Take(index).Sum(p => p.Host.Height + PaperInset) + point.Y * scale;
        _scroll.ChangeView(null, Math.Max(0, y - 100), null, true);
        Mark(index, point.X, point.Y - 8, 16 / scale, 16 / scale);
        ScheduleRender();
    }
    private Point PagePoint(int page, Grid host, PointerRoutedEventArgs args)
    { var p = args.GetCurrentPoint(host).Position; var scale = _document!.Pages[page].Width / host.Width; return new(p.X * scale, p.Y * scale); }
    private void Press(int page, Grid host, PointerRoutedEventArgs args)
    {
        if (_document == null || !args.GetCurrentPoint(host).Properties.IsLeftButtonPressed) return;
        host.Focus(FocusState.Pointer);
        var point = PagePoint(page, host, args);
        if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) { SourceRequested?.Invoke(page, point.X, point.Y); args.Handled = true; return; }
        _selectionStart = (page, point); host.CapturePointer(args.Pointer);
    }
    private async void Release(int page, Grid host, PointerRoutedEventArgs args)
    {
        host.ReleasePointerCapture(args.Pointer);
        if (_document == null || _selectionStart is not { } start || start.Page != page) return;
        _selectionStart = null; var document = _document; var generation = _generation; var end = PagePoint(page, host, args);
        try
        {
            var selection = await Task.Run(() => document.Select(page, start.Point.X, start.Point.Y, end.X, end.Y));
            if (generation != _generation || selection == null) return;
            _selection = selection; _selectionPage = page;
            foreach (var p in _pages) p.Marker.Children.Clear();
            foreach (var rect in selection.Rectangles) AddMarker(page, rect.Left, rect.Top, rect.Width, rect.Height);
        }
        catch (ObjectDisposedException) { } catch (Exception error) { Error?.Invoke(error.Message); }
    }
    private void Mark(int page, double x, double y, double width, double height)
    {
        foreach (var p in _pages) p.Marker.Children.Clear();
        AddMarker(page, x, y, width, height);
    }
    private void AddMarker(int page, double x, double y, double width, double height)
    {
        var scale = _pages[page].Host.Width / _document!.Pages[page].Width;
        var marker = new Rectangle { Stroke = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue), StrokeThickness = 1,
            Fill = Ui.Brush(this, "AccentFillColorDefaultBrush"), Opacity = Ui.D("SelectionOpacity"), Width = Math.Max(3, width * scale), Height = Math.Max(3, height * scale) };
        Canvas.SetLeft(marker, x * scale); Canvas.SetTop(marker, y * scale); _pages[page].Marker.Children.Add(marker);
    }
    private void CopySelection()
    {
        if (_selection == null) return;
        try { var data = new Windows.ApplicationModel.DataTransfer.DataPackage(); data.SetText(_selection.Text); Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data); } catch (Exception error) { Error?.Invoke(error.Message); }
    }
    public bool NavigateSelection()
    {
        if (_selection == null) return false;
        SourceRequested?.Invoke(_selectionPage, _selection.Left + _selection.Width / 2, _selection.Top + _selection.Height / 2); return true;
    }
    public async Task WaitForRenderAsync() { await _renderGate.WaitAsync(); _renderGate.Release(); }
    public void Close() { _generation++; _render.Cancel(); _stack.Children.Clear(); _pages.Clear(); _renderedWidths.Clear(); var document = _document; _document = null; PageChanged?.Invoke(0, 0); if (document != null) _ = Task.Run(document.Dispose); }
}
