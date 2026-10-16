using System.Diagnostics;
using TypstStudio.Core;
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
var root = Path.Combine(Path.GetTempPath(), "TypstStudio-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var main = Path.Combine(root, "main.typ"); var child = Path.Combine(root, "chapter.typ");
    var mainText = "= Main\n$ alpha + sqrt(2) $\n$ frac(1, 2) + integral_0^1 x dif x + sum_(i=0)^3 i $\n#include \"chapter.typ\"";
    var childText = "#pagebreak()\n= 中文章节\n中文 $ alpha + sqrt(2) $\n#rotate(20deg)[$ beta $]";
    await File.WriteAllTextAsync(main, mainText); await File.WriteAllTextAsync(child, childText);
    var bufferPath = Path.Combine(root, "buffer.typ"); await File.WriteAllTextAsync(bufferPath, "one\r\ntwo\r\n");
    var buffer = new SourceBuffer(bufferPath, await File.ReadAllTextAsync(bufferPath));
    Check(buffer.Text == "one\ntwo\n", "Normalize CRLF for editor coordinates");
    buffer.Text += "three\n"; await buffer.SaveAsync(); Check((await File.ReadAllTextAsync(bufferPath)).Contains("three\r\n"), "Preserve original newline style");
    await File.WriteAllTextAsync(bufferPath, "external change"); buffer.Text += "local";
    try { await buffer.SaveAsync(); throw new Exception("Overwrote external file"); } catch (IOException) { }
    Check(await File.ReadAllTextAsync(bufferPath) == "external change", "External changes preserved");
    Check(ProjectFiles.MainFile(ProjectFiles.Sources(root)) == main, "Main discovery");
    Check(!ProjectFiles.Inside(root, Path.Combine(root, "..", "outside.typ")), "Root boundary");
    var unicode = "🙂e\u0301中文 alpha"; var start = unicode.IndexOf("alpha", StringComparison.Ordinal); var position = SourceCoordinates.Position(unicode, start);
    Check(SourceCoordinates.Offset(unicode, position.Line, position.Column) == start, "Unicode scalar/UTF16 roundtrip");
    Console.WriteLine("PASS: project discovery, Unicode coordinates, atomic save, CRLF and conflict preservation");
    await using var engine = new TinymistSession(Path.Combine(AppContext.BaseDirectory, "tinymist.exe"), root, main);
    await engine.InitializeAsync(); var documents = new Dictionary<string, string> { [main] = mainText };
    var timer = Stopwatch.StartNew(); var pdf = await engine.CompileAsync(documents, CancellationToken.None); Console.WriteLine($"Initial compile: {timer.ElapsedMilliseconds} ms");
    using (var document = new PdfDocument(pdf))
    {
        Check(document.Pages.Count == 2, "PDF page count");
        var pixels = document.Render(1, 900, CancellationToken.None);
        Check(pixels.Bgra.Length == pixels.Width * pixels.Height * 4 && pixels.Bgra.Count(b => b < 200) > 100, "Native PDF pixels");
    }
    foreach (var token in new[] { "alpha", "sqrt", "frac", "integral", "sum" })
    {
        var p = SourceCoordinates.Position(mainText, mainText.IndexOf(token, StringComparison.Ordinal));
        var mapped = await engine.ForwardAsync(main, p.Line, p.Column, CancellationToken.None);
        Check(mapped.Points.Length > 0 && mapped.Points[0].Page == 1, "Formula mapping: " + token);
    }
    var childPosition = SourceCoordinates.Position(childText, childText.IndexOf("alpha", StringComparison.Ordinal));
    var childMap = await engine.ForwardAsync(child, childPosition.Line, childPosition.Column, CancellationToken.None);
    var target = childMap.Points.First(); Check(target.Page == 2, "Repeated formula must map to included file");
    var source = await engine.InverseAsync(2, target.X + .5, target.Y - 1, CancellationToken.None);
    Check(source.Path.Equals(child, StringComparison.OrdinalIgnoreCase), "Inverse file");
    Check(SourceCoordinates.Offset(childText, source.Line, source.Column) == childText.IndexOf("alpha", StringComparison.Ordinal), "Inverse exact token");
    using (var document = new PdfDocument(childMap.Pdf)) Check(!string.IsNullOrWhiteSpace(document.Select(1, target.X + 1, target.Y - 3, target.X + 3, target.Y - 3)?.Text), "PDF text hit/selection");
    var rotatedPosition = SourceCoordinates.Position(childText, childText.IndexOf("beta", StringComparison.Ordinal));
    var rotated = (await engine.ForwardAsync(child, rotatedPosition.Line, rotatedPosition.Column, CancellationToken.None)).Points.First();
    var rotatedSource = await engine.InverseAsync(rotated.Page, rotated.X + .5, rotated.Y - 1, CancellationToken.None);
    Check(rotatedSource.Line == 3, "Rotated formula mapping");
    Console.WriteLine("PASS: PDFium rendering/text selection, formulas, exact repeated formulas, included files, rotated frames");
    documents[child] = childText + "\n#pagebreak()\n= Unsaved content";
    var updated = await engine.CompileAsync(documents, CancellationToken.None);
    using (var document = new PdfDocument(updated)) Check(document.Pages.Count == 3, "In-memory included edit");
    Check(await File.ReadAllTextAsync(child) == childText, "Never write unsaved source during compilation");
    documents[child] = "#set text(font: )";
    try { await engine.CompileAsync(documents, CancellationToken.None); throw new Exception("Invalid source exported"); } catch (InvalidOperationException) { }
    documents[child] = childText; var recovered = await engine.CompileAsync(documents, CancellationToken.None);
    using (var document = new PdfDocument(recovered)) Check(document.Pages.Count == 2, "Recover after compiler error");
    Console.WriteLine("PASS: unsaved dependencies, compiler errors and recovery");
    Console.WriteLine("ARCHITECTURE: " + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
}
finally { Directory.Delete(root, recursive: true); }
