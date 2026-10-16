using System.Runtime.InteropServices;
namespace TypstStudio.Core;
public record PageSize(double Width, double Height);
public record PdfPixels(int Width, int Height, byte[] Bgra);
public record SelectionRect(double Left, double Top, double Width, double Height);
public record PdfTextSelection(string Text, double Left, double Top, double Width, double Height, IReadOnlyList<SelectionRect> Rectangles);

/// PDFium calls are serialized, including disposal. UI receives immutable buffers only.
public sealed class PdfDocument : IDisposable
{
    private static readonly object Gate = new();
    private static bool _initialized;
    private nint _document;
    private GCHandle _data;
    public IReadOnlyList<PageSize> Pages { get; }
    public PdfDocument(byte[] bytes)
    {
        lock (Gate)
        {
            if (!_initialized) { Native.FPDF_InitLibrary(); _initialized = true; }
            _data = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            _document = Native.FPDF_LoadMemDocument64(_data.AddrOfPinnedObject(), (nuint)bytes.Length, null);
            if (_document == 0) { _data.Free(); throw new InvalidDataException("无法读取编译后的 PDF。"); }
            var pages = new List<PageSize>();
            for (var i = 0; i < Native.FPDF_GetPageCount(_document); i++)
            {
                if (Native.FPDF_GetPageSizeByIndex(_document, i, out var width, out var height) == 0 || !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
                { Native.FPDF_CloseDocument(_document); _document = 0; _data.Free(); throw new InvalidDataException("无效的 PDF 页面尺寸。"); }
                pages.Add(new(width, height));
            }
            Pages = pages;
        }
    }
    public IReadOnlyList<PdfHeading> ReadOutline()
    {
        lock (Gate)
        {
            ObjectDisposedException.ThrowIf(_document == 0, this);
            var visited = new HashSet<nint>();
            List<PdfHeading> Read(nint parent, int depth)
            {
                var items = new List<PdfHeading>(); if (depth > 64) return items;
                for (var mark = Native.FPDFBookmark_GetFirstChild(_document, parent); mark != 0 && visited.Count < 5000 && visited.Add(mark); mark = Native.FPDFBookmark_GetNextSibling(_document, mark))
                {
                    var count = Native.FPDFBookmark_GetTitle(mark, null, 0); if (count > 65536) continue;
                    var bytes = new byte[count]; Native.FPDFBookmark_GetTitle(mark, bytes, count);
                    var title = System.Text.Encoding.Unicode.GetString(bytes).TrimEnd('\0');
                    var dest = Native.FPDFBookmark_GetDest(_document, mark); PdfPoint? location = null;
                    if (dest != 0)
                    {
                        var page = Native.FPDFDest_GetDestPageIndex(_document, dest);
                        if (page >= 0 && page < Pages.Count)
                        {
                            var found = Native.FPDFDest_GetLocationInPage(dest, out var hasX, out var hasY, out _, out var x, out var y, out _);
                            location = new(page + 1, found != 0 && hasX != 0 && float.IsFinite(x) ? x : 0, found != 0 && hasY != 0 && float.IsFinite(y) ? Math.Clamp(Pages[page].Height - y, 0, Pages[page].Height) : 0);
                        }
                    }
                    items.Add(new(title, location, Read(mark, depth + 1)));
                }
                return items;
            }
            return Read(0, 0);
        }
    }
    public PdfPixels Render(int index, int width, CancellationToken token)
    {
        lock (Gate)
        {
            ObjectDisposedException.ThrowIf(_document == 0, this); token.ThrowIfCancellationRequested();
            var size = Pages[index]; width = Math.Clamp(width, 100, 4096);
            var height = Math.Clamp((int)Math.Ceiling(width * size.Height / size.Width), 1, 8192);
            var bytes = new byte[checked(width * height * 4)];
            var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            var bitmap = Native.FPDFBitmap_CreateEx(width, height, 4, pinned.AddrOfPinnedObject(), width * 4);
            var page = Native.FPDF_LoadPage(_document, index);
            try
            {
                if (bitmap == 0 || page == 0) throw new InvalidDataException("无法渲染 PDF 页面。");
                Native.FPDFBitmap_FillRect(bitmap, 0, 0, width, height, 0xffffffff);
                Native.FPDF_RenderPageBitmap(bitmap, page, 0, 0, width, height, 0, 1);
                token.ThrowIfCancellationRequested(); return new(width, height, bytes);
            }
            finally { Native.FPDF_ClosePage(page); Native.FPDFBitmap_Destroy(bitmap); pinned.Free(); }
        }
    }
    public PdfTextSelection? Select(int index, double x1, double y1, double x2, double y2)
    {
        lock (Gate)
        {
            ObjectDisposedException.ThrowIf(_document == 0, this);
            var page = Native.FPDF_LoadPage(_document, index); var text = Native.FPDFText_LoadPage(page);
            try
            {
                var height = Pages[index].Height;
                var a = Native.FPDFText_GetCharIndexAtPos(text, x1, height - y1, 8, 8);
                var b = Native.FPDFText_GetCharIndexAtPos(text, x2, height - y2, 8, 8);
                if (a < 0 || b < 0) return null;
                if (a > b) (a, b) = (b, a);
                var chars = new ushort[checked((b - a + 1) * 2 + 2)];
                var count = Native.FPDFText_GetText(text, a, b - a + 1, chars);
                var value = new string(chars.Take(Math.Max(0, count - 1)).Select(c => (char)c).ToArray());
                Native.FPDFText_GetCharBox(text, a, out var left, out var right, out var bottom, out var top);
                var rectangles = new List<SelectionRect>();
                var rectangleCount = Native.FPDFText_CountRects(text, a, b - a + 1);
                for (var i = 0; i < rectangleCount; i++)
                {
                    Native.FPDFText_GetRect(text, i, out var l, out var t, out var r, out var bot);
                    rectangles.Add(new(l, height - t, r - l, t - bot));
                }
                return new(value, left, height - top, Math.Max(2, right - left), Math.Max(2, top - bottom), rectangles);
            }
            finally { Native.FPDFText_ClosePage(text); Native.FPDF_ClosePage(page); }
        }
    }
    public void Dispose() { lock (Gate) { if (_document != 0) { Native.FPDF_CloseDocument(_document); _document = 0; _data.Free(); } } }
    private static class Native
    {
        private const string Dll = "pdfium";
        [DllImport(Dll)] internal static extern nint FPDFBookmark_GetFirstChild(nint document, nint bookmark);
        [DllImport(Dll)] internal static extern nint FPDFBookmark_GetNextSibling(nint document, nint bookmark);
        [DllImport(Dll)] internal static extern uint FPDFBookmark_GetTitle(nint bookmark, [Out] byte[]? buffer, uint length);
        [DllImport(Dll)] internal static extern nint FPDFBookmark_GetDest(nint document, nint bookmark);
        [DllImport(Dll)] internal static extern int FPDFDest_GetDestPageIndex(nint document, nint dest);
        [DllImport(Dll)] internal static extern int FPDFDest_GetLocationInPage(nint dest, out int hasX, out int hasY, out int hasZoom, out float x, out float y, out float zoom);
        [DllImport(Dll)] internal static extern void FPDF_InitLibrary();
        [DllImport(Dll)] internal static extern nint FPDF_LoadMemDocument64(nint data, nuint size, [MarshalAs(UnmanagedType.LPStr)] string? password);
        [DllImport(Dll)] internal static extern void FPDF_CloseDocument(nint document);
        [DllImport(Dll)] internal static extern int FPDF_GetPageCount(nint document);
        [DllImport(Dll)] internal static extern nint FPDF_LoadPage(nint document, int index);
        [DllImport(Dll)] internal static extern void FPDF_ClosePage(nint page);
        [DllImport(Dll)] internal static extern int FPDF_GetPageSizeByIndex(nint document, int index, out double width, out double height);
        [DllImport(Dll)] internal static extern nint FPDFBitmap_CreateEx(int width, int height, int format, nint buffer, int stride);
        [DllImport(Dll)] internal static extern void FPDFBitmap_FillRect(nint bitmap, int left, int top, int width, int height, uint color);
        [DllImport(Dll)] internal static extern void FPDFBitmap_Destroy(nint bitmap);
        [DllImport(Dll)] internal static extern void FPDF_RenderPageBitmap(nint bitmap, nint page, int left, int top, int width, int height, int rotate, int flags);
        [DllImport(Dll)] internal static extern nint FPDFText_LoadPage(nint page);
        [DllImport(Dll)] internal static extern void FPDFText_ClosePage(nint page);
        [DllImport(Dll)] internal static extern int FPDFText_GetCharIndexAtPos(nint page, double x, double y, double xTolerance, double yTolerance);
        [DllImport(Dll)] internal static extern int FPDFText_GetText(nint page, int start, int count, [Out] ushort[] result);
        [DllImport(Dll)] internal static extern int FPDFText_CountRects(nint page, int start, int count);
        [DllImport(Dll)] internal static extern int FPDFText_GetRect(nint page, int index, out double left, out double top, out double right, out double bottom);
        [DllImport(Dll)] internal static extern int FPDFText_GetCharBox(nint page, int index, out double left, out double right, out double bottom, out double top);
    }
}
