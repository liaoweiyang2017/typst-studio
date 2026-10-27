using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text;
using TypstStudio.Core;
using WinUIEditor;

namespace TypstStudio.App;

// Keeps Scintilla byte offsets out of the rest of the application (which uses UTF-16).
public sealed partial class SourceEditor : UserControl
{
    private EditorBaseControl _view => NativeView;
    private Editor Native => _view.Editor;
    private readonly DispatcherTimer _highlight = new() { Interval = TimeSpan.FromMilliseconds(60) };
    private readonly DispatcherTimer _navigationFlash = new() { Interval = TimeSpan.FromSeconds(3) };
    private long _navigationPosition = -1;
    private string _text = "";
    private bool _ready, _readOnly = true, _numbers = true, _wrap = true;
    public event EventHandler<RoutedEventArgs>? TextChanged;
    public event EventHandler<RoutedEventArgs>? SelectionChanged;
    public SourceEditor()
    {
        InitializeComponent();
        ActualThemeChanged += (_, _) => ApplyStyle();
        RegisterPropertyChangedCallback(FontSizeProperty, (_, _) => ApplyStyle());
        _highlight.Tick += (_, _) => { _highlight.Stop(); Highlight(); };
        _navigationFlash.Tick += (_, _) => ClearNavigationMark();
    }
    public void ActivateEditor() => FindName("NativeView");
    public new bool Focus(FocusState state) => _ready && _view.Focus(state);
    private void NativeView_Loaded(object sender, RoutedEventArgs args)
    {
            if (_ready) return; _ready = true;
            Native.CodePage = 65001; Native.UseTabs = false; Native.TabWidth = 2; Native.ScrollWidth = 1; Native.ScrollWidthTracking = true;
            Native.SetMarginTypeN(0, MarginType.Number); Native.SetMarginWidthN(1, 0); Native.SetMarginWidthN(2, 0);
            Native.Modified += (_, e) => { if ((e.ModificationType & 3) == 0) return; ClearNavigationMark(); _text = Native.GetText(Native.TextLength + 1).TrimEnd('\0'); _highlight.Stop(); _highlight.Start(); TextChanged?.Invoke(this, new RoutedEventArgs()); };
            Native.UpdateUI += (_, e) => { if ((e.Updated & 2) != 0) SelectionChanged?.Invoke(this, new RoutedEventArgs()); UpdateActiveLine(); };
            var text = _text; Native.ReadOnly = false; Native.SetText(text); Native.EmptyUndoBuffer(); Native.ReadOnly = _readOnly; ApplyStyle();
    }
    public string Text { get => _text; set { _text = SourceBuffer.Normalize(value); if (!_ready) return; var readOnly = Native.ReadOnly; Native.ReadOnly = false; Native.SetText(_text); Native.ReadOnly = readOnly; Highlight(); } }
    public bool IsReadOnly { get => _readOnly; set { _readOnly = value; if (_ready) Native.ReadOnly = value; } }
    public int SelectionStart => _ready ? Utf16(Native.SelectionStart) : 0;
    public int SelectionLength => _ready ? Utf16(Native.SelectionEnd) - SelectionStart : 0;
    public string SelectedText { get => _text.Substring(SelectionStart, SelectionLength); set { if (_ready && !IsReadOnly) Native.ReplaceSel(value); } }
    public bool ShowLineNumbers { get => _numbers; set { _numbers = value; ApplyStyle(); } }
    public bool WordWrap { get => _wrap; set { _wrap = value; ApplyStyle(); } }
    public void Select(int start, int length) { if (_ready) { Native.SetSel(Bytes(start), Bytes(start + length)); Native.ScrollCaret(); } }
    public void FlashNavigation()
    {
        _navigationPosition = Native.CurrentPos; _navigationFlash.Stop(); _navigationFlash.Start();
        DispatcherQueue.TryEnqueue(UpdateNavigationMark);
    }
    private void ClearNavigationMark() { _navigationFlash.Stop(); _navigationPosition = -1; NavigationHighlight.Visibility = Visibility.Collapsed; }
    private void UpdateNavigationMark()
    {
        if (_navigationPosition < 0) return;
        var y = Native.PointYFromPosition(_navigationPosition) / XamlRoot.RasterizationScale;
        NavigationHighlight.Visibility = y >= 0 && y < ActualHeight ? Visibility.Visible : Visibility.Collapsed;
        NavigationHighlight.Margin = new Thickness(0, Math.Max(0, y), 0, 0); NavigationHighlight.Height = LineHeight;
    }
    internal bool HasNavigationMark => _navigationFlash.IsEnabled && NavigationHighlight.Visibility == Visibility.Visible;
    public void ClearUndoRedoHistory() { if (_ready) Native.EmptyUndoBuffer(); }
    public void Undo() { if (_ready) Native.Undo(); }
    public void Redo() { if (_ready) Native.Redo(); }
    public void ToggleComment()
    {
        if (!_ready || IsReadOnly) return;
        var change = TypstEditing.ToggleComment(_text, SelectionStart, SelectionLength);
        Native.BeginUndoAction(); try { Select(change.Start, change.Length); Native.ReplaceSel(change.Text); Select(change.Start, change.Text.Length); } finally { Native.EndUndoAction(); }
    }
    private long Bytes(int offset) => Encoding.UTF8.GetByteCount(_text.AsSpan(0, Math.Clamp(offset, 0, _text.Length)));
    private int Utf16(long bytes) { var data = Encoding.UTF8.GetBytes(_text); return Encoding.UTF8.GetCharCount(data.AsSpan(0, Math.Clamp((int)bytes, 0, data.Length))); }
    public void ReplaceDocumentAsEdit(string text, int caret)
    {
        if (!_ready || IsReadOnly) return;
        Native.BeginUndoAction(); try { Native.SetSel(0, Native.TextLength); Native.ReplaceSel(text); Select(Math.Min(caret, Text.Length), 0); } finally { Native.EndUndoAction(); }
    }
    private static int Color(Windows.UI.Color c) => c.R | (c.G << 8) | (c.B << 16);
    public void ApplyStyle()
    {
        if (!_ready) return;
        var bg = Ui.OpaqueColor(this, "LayerFillColorDefaultBrush"); var fg = Ui.OpaqueColor(this, "TextFillColorPrimaryBrush", bg);
        Native.StyleSetFore(32, Color(fg)); Native.StyleSetBack(32, Color(bg)); Native.StyleSetFont(32, ((Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["StudioCodeFont"]).Source); Native.StyleSetSizeFractional(32, (int)(FontSize * 75)); Native.StyleClearAll();
        Native.ExtraAscent = Native.ExtraDescent = 0;
        var extra = Math.Max(0, (int)Math.Round(FontSize * 1.5 * (XamlRoot?.RasterizationScale ?? 1)) - Native.TextHeight(0));
        Native.ExtraAscent = extra / 2; Native.ExtraDescent = extra - extra / 2;
        string[] colors = ["TextFillColorTertiaryBrush", "SystemFillColorSuccessBrush", "AccentTextFillColorPrimaryBrush", "AccentTextFillColorSecondaryBrush", "AccentTextFillColorPrimaryBrush", "SystemFillColorCautionBrush", "TextFillColorPrimaryBrush"];
        for (var i = 0; i < colors.Length; i++) Native.StyleSetFore(i + 1, Color(Ui.OpaqueColor(this, colors[i], bg)));
        Native.StyleSetBack(33, Color(bg)); Native.SetFoldMarginColour(true, Color(bg)); Native.SetFoldMarginHiColour(true, Color(bg)); Native.MarginLeft = (int)Ui.D("Space12"); Native.MarginRight = (int)Ui.D("Space12");
        Native.StyleSetBold(3, true); Native.StyleSetBold(7, true); Native.StyleSetFore(33, Color(Ui.OpaqueColor(this, "TextFillColorTertiaryBrush", bg)));
        Native.CaretFore = Color(fg); Native.SetSelBack(true, Color(Ui.OpaqueColor(this, "AccentFillColorDefaultBrush", bg))); Native.SetMarginWidthN(0, _numbers ? (int)(Ui.D("EditorGutterWidth") * (XamlRoot?.RasterizationScale ?? 1)) : 0); Native.WrapMode = _wrap ? Wrap.Word : Wrap.None; Native.HScrollBar = !_wrap;
        Native.CaretLineVisible = true; Native.CaretLineBack = Color(Ui.OpaqueColor(this, "SubtleFillColorSecondaryBrush", bg));
        ActiveLineNumberHost.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(bg); ActiveLineNumber.FontSize = FontSize;
        Highlight(); UpdateActiveLine();
    }
    private void UpdateActiveLine()
    {
        UpdateNavigationMark();
        if (!_ready || !_numbers) { ActiveLineNumberHost.Visibility = Visibility.Collapsed; return; }
        var line = Native.LineFromPosition(Native.CurrentPos); var scale = XamlRoot?.RasterizationScale ?? 1;
        var y = Native.PointYFromPosition(Native.PositionFromLine(line)) / scale;
        ActiveLineNumberHost.Visibility = y >= 0 && y < ActualHeight ? Visibility.Visible : Visibility.Collapsed;
        ActiveLineNumberHost.Margin = new Thickness(0, Math.Max(0, y), 0, 0); // Native document geometry, not UI spacing.
        ActiveLineNumberHost.Width = Ui.D("EditorGutterWidth"); ActiveLineNumberHost.Height = Native.TextHeight(line) / scale; ActiveLineNumber.Text = (line + 1).ToString();
    }
    private void Highlight()
    {
        if (!_ready) return;
        Native.StartStyling(0, 0); Native.SetStyling(Native.TextLength, 0);
        var position = 0; long bytes = 0;
        foreach (var span in TypstEditing.Highlight(_text)) { bytes += Encoding.UTF8.GetByteCount(_text.AsSpan(position, span.Start - position)); var count = Encoding.UTF8.GetByteCount(_text.AsSpan(span.Start, span.Length)); Native.StartStyling(bytes, 0); Native.SetStyling(count, span.Style); bytes += count; position = span.Start + span.Length; }
    }
    internal double LineHeight => Native.TextHeight(0) / XamlRoot.RasterizationScale;
    internal bool HasHighlight => _ready && TypstEditing.Highlight(_text).Any(s => Native.GetStyleAt(Bytes(s.Start)) == s.Style);
}
