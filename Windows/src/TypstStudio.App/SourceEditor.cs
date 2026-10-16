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
    private string _text = "";
    private bool _ready, _readOnly = true, _numbers = true, _wrap;
    public event EventHandler<RoutedEventArgs>? TextChanged;
    public event EventHandler<RoutedEventArgs>? SelectionChanged;
    public SourceEditor()
    {
        InitializeComponent();
        ActualThemeChanged += (_, _) => ApplyStyle();
        RegisterPropertyChangedCallback(FontSizeProperty, (_, _) => ApplyStyle());
        _highlight.Tick += (_, _) => { _highlight.Stop(); Highlight(); };
    }
    public void ActivateEditor() => FindName("NativeView");
    public new bool Focus(FocusState state) => _ready && _view.Focus(state);
    private void NativeView_Loaded(object sender, RoutedEventArgs args)
    {
            if (_ready) return; _ready = true;
            Native.CodePage = 65001; Native.UseTabs = false; Native.TabWidth = 2; Native.ScrollWidth = 1; Native.ScrollWidthTracking = true;
            Native.SetMarginTypeN(0, MarginType.Number); Native.SetMarginWidthN(1, 0); Native.SetMarginWidthN(2, 0);
            Native.Modified += (_, e) => { if ((e.ModificationType & 3) == 0) return; _text = Native.GetText(Native.TextLength + 1).TrimEnd('\0'); _highlight.Stop(); _highlight.Start(); TextChanged?.Invoke(this, new RoutedEventArgs()); };
            Native.UpdateUI += (_, e) => { if ((e.Updated & 2) != 0) SelectionChanged?.Invoke(this, new RoutedEventArgs()); };
            var text = _text; Native.ReadOnly = false; Native.SetText(text); Native.EmptyUndoBuffer(); Native.ReadOnly = _readOnly; ApplyStyle();
    }
    public string Text { get => _text; set { _text = SourceBuffer.Normalize(value); if (!_ready) return; var readOnly = Native.ReadOnly; Native.ReadOnly = false; Native.SetText(_text); Native.ReadOnly = readOnly; Highlight(); } }
    public bool IsReadOnly { get => _readOnly; set { _readOnly = value; if (_ready) Native.ReadOnly = value; } }
    public int SelectionStart => _ready ? Utf16(Native.SelectionStart) : 0;
    public int SelectionLength => _ready ? Utf16(Native.SelectionEnd) - SelectionStart : 0;
    public string SelectedText { set { if (_ready && !IsReadOnly) Native.ReplaceSel(value); } }
    public bool ShowLineNumbers { get => _numbers; set { _numbers = value; ApplyStyle(); } }
    public bool WordWrap { get => _wrap; set { _wrap = value; ApplyStyle(); } }
    public void Select(int start, int length) { if (_ready) { Native.SetSel(Bytes(start), Bytes(start + length)); Native.ScrollCaret(); } }
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
    private static int Color(int rgb) => ((rgb & 255) << 16) | (rgb & 0xff00) | ((rgb >> 16) & 255);
    public void ApplyStyle()
    {
        if (!_ready) return;
        var dark = ActualTheme == ElementTheme.Dark; var bg = Color(dark ? 0x202020 : 0xffffff); var fg = Color(dark ? 0xdedede : 0x262626);
        Native.StyleSetFore(32, fg); Native.StyleSetBack(32, bg); Native.StyleSetFont(32, "Cascadia Mono"); Native.StyleSetSizeFractional(32, (int)(FontSize * 75)); Native.StyleClearAll();
        int[] colors = dark ? [0x939393, 0x83c989, 0xf08080, 0x9aaafa, 0x7ecbd7, 0xd8b87c, 0xe1c797] : [0x808080, 0x207b35, 0xc42b42, 0x455bb6, 0x087d91, 0x986300, 0x965c18];
        for (var i = 0; i < colors.Length; i++) Native.StyleSetFore(i + 1, Color(colors[i]));
        Native.StyleSetBack(33, bg); Native.SetFoldMarginColour(true, bg); Native.SetFoldMarginHiColour(true, bg); Native.MarginLeft = 12; Native.MarginRight = 12;
        Native.StyleSetBold(3, true); Native.StyleSetBold(7, true); Native.StyleSetFore(33, Color(dark ? 0x858585 : 0x949494));
        Native.CaretFore = fg; Native.SetSelBack(true, Color(dark ? 0x264f78 : 0xcce8ff)); Native.SetMarginWidthN(0, _numbers ? (int)(40 * (XamlRoot?.RasterizationScale ?? 1)) : 0); Native.WrapMode = _wrap ? Wrap.Word : Wrap.None;
        Highlight();
    }
    private void Highlight()
    {
        if (!_ready) return;
        Native.StartStyling(0, 0); Native.SetStyling(Native.TextLength, 0);
        var position = 0; long bytes = 0;
        foreach (var span in TypstEditing.Highlight(_text)) { bytes += Encoding.UTF8.GetByteCount(_text.AsSpan(position, span.Start - position)); var count = Encoding.UTF8.GetByteCount(_text.AsSpan(span.Start, span.Length)); Native.StartStyling(bytes, 0); Native.SetStyling(count, span.Style); bytes += count; position = span.Start + span.Length; }
    }
    internal bool HasHighlight => _ready && TypstEditing.Highlight(_text).Any(s => Native.GetStyleAt(Bytes(s.Start)) == s.Style);
}
