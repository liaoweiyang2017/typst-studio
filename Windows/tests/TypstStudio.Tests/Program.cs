using System.Diagnostics;
using TypstStudio.Core;
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
var commentSource = "  中文 $ alpha $\n  #let x = 2\nnext";
var comment = TypstEditing.ToggleComment(commentSource, 0, commentSource.IndexOf("next", StringComparison.Ordinal));
var commented = commentSource.Remove(comment.Start, comment.Length).Insert(comment.Start, comment.Text);
Check(commented == "  // 中文 $ alpha $\n  // #let x = 2\nnext", "Comment selection ending at next line start");
var uncomment = TypstEditing.ToggleComment(commented, 0, commented.IndexOf("next", StringComparison.Ordinal));
Check(commented.Remove(uncomment.Start, uncomment.Length).Insert(uncomment.Start, uncomment.Text) == commentSource, "Comment toggle round trip");
var spans = TypstEditing.Highlight("#let a = \"/* string */\"\n/* outer /* nested */ end */\n$ alpha + 2 $\n= 标题");
Check(spans.Any(s => s.Style == 1) && spans.Any(s => s.Style == 2) && spans.Any(s => s.Style == 3) && spans.Any(s => s.Style == 5) && spans.Any(s => s.Style == 7), "Typst highlights strings, nested comments, keyword, math and headings");
Console.WriteLine("PASS: syntax spans and reversible multi-line comments");
await UpdateTests.RunAsync();
var root = Path.Combine(Path.GetTempPath(), "TypstStudio-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var main = Path.Combine(root, "main.typ"); var child = Path.Combine(root, "chapter.typ");
    var mainText = "= Main\n$ alpha + sqrt(2) $\n$ frac(1, 2) + integral_0^1 x dif x + sum_(i=0)^3 i $\n#include \"chapter.typ\"";
    var childText = "#pagebreak()\n= 中文章节\n中文 $ alpha + sqrt(2) $\n#rotate(20deg)[$ beta $]\n== Nested\n=== Detail";
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
    var search = await ProjectSearch.FindAsync([child], new Dictionary<string, string> { [child] = "中文\nUNSAVED needle" }, "needle", false, CancellationToken.None);
    Check(search.Count == 1 && search[0].Line == 1 && search[0].Offset == 11, "Search unsaved Unicode source with exact UTF16 offset");
    Check((await ProjectSearch.FindAsync([child], new Dictionary<string, string> { [child] = "Needle" }, "needle", true, CancellationToken.None)).Count == 0, "Case-sensitive project search");
    Check((await ProjectSearch.FindAsync([child], new Dictionary<string, string> { [child] = string.Join("\n", Enumerable.Repeat("needle", 250)) }, "needle", false, CancellationToken.None)).Count == 200, "Bounded search results");
    Check(MathSymbols.Insertion("$ x ", 4, "alpha").Trim() == "alpha", "Symbol inside math");
    Check(MathSymbols.Insertion("`$` ", 4, "alpha") == "$ alpha $", "Raw dollar does not open math");
    await File.WriteAllTextAsync(Path.Combine(root, ".typst-studio-live-test.typ"), "temporary");
    Check(!ProjectFiles.Sources(root).Any(p => Path.GetFileName(p).StartsWith(".typst-studio-")), "Generated snapshots are hidden");
    Console.WriteLine("PASS: project search, symbol insertion and hidden snapshots");
    await using var engine = new TinymistSession(Path.Combine(AppContext.BaseDirectory, "tinymist.exe"), root, main);
    await engine.InitializeAsync(); var documents = new Dictionary<string, string> { [main] = mainText };
    var output = new PdfOutput(); var outputPath = Path.ChangeExtension(main, ".pdf");
    await output.WriteAsync(main, "test PDF bytes"u8.ToArray());
    var written = await File.ReadAllBytesAsync(outputPath);
    var modified = File.GetLastWriteTimeUtc(outputPath);
    await output.WriteAsync(main, written); Check(File.GetLastWriteTimeUtc(outputPath) == modified, "Unchanged PDF skips write");
    File.SetAttributes(outputPath, FileAttributes.ReadOnly);
    try { await output.WriteAsync(main, "replacement"u8.ToArray()); throw new Exception("Overwrote read-only PDF"); } catch (IOException) { }
    Check((await File.ReadAllBytesAsync(outputPath)).AsSpan().SequenceEqual(written), "Failed PDF write retains old output");
    File.SetAttributes(outputPath, FileAttributes.Normal); File.Delete(outputPath);
    Console.WriteLine("PASS: atomic same-directory PDF output and readonly preservation");
    var timer = Stopwatch.StartNew(); var pdf = await engine.CompileAsync(documents, CancellationToken.None); Console.WriteLine($"Initial compile: {timer.ElapsedMilliseconds} ms");
    using (var document = new PdfDocument(pdf))
    {
        Check(document.Pages.Count == 2, "PDF page count");
        var outline = document.ReadOutline();
        Check(outline.Count >= 2 && outline.Any(h => h.Title.Contains("中文") && h.Destination?.Page == 2), "PDF outline includes cross-file Unicode heading and destination");
        Check(outline.Any(h => h.Children.Any(c => c.Children.Count > 0)), "PDF outline preserves heading levels");
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
    var standalone = Path.Combine(root, "standalone.typ");
    var standaloneText = "= Standalone\n$ gamma $";
    documents[standalone] = standaloneText;
    var standalonePdf = await engine.CompileAsync(documents, CancellationToken.None, standalone);
    using (var document = new PdfDocument(standalonePdf)) Check(document.Pages.Count == 1 && engine.Entry == standalone, "Explicit entry switch");
    var standaloneMap = await engine.ForwardAsync(standalone, 1, 2, CancellationToken.None);
    var standalonePoint = standaloneMap.Points.First();
    Check((await engine.InverseAsync(standalonePoint.Page, standalonePoint.X + .5, standalonePoint.Y - 1, CancellationToken.None)).Path == standalone, "Mapping task follows explicit entry");
    await engine.CompileAsync(documents, CancellationToken.None, main);
    var back = (await engine.ForwardAsync(child, childPosition.Line, childPosition.Column, CancellationToken.None)).Points.First();
    Check(back.Page == 2 && (await engine.InverseAsync(back.Page, back.X + .5, back.Y - 1, CancellationToken.None)).Path == child, "Restored main entry mapping");
    Console.WriteLine("PASS: explicit entry switches reuse compiler and rebuild exact mapping task");
    documents[child] = childText + "\n#pagebreak()\n= Unsaved content";
    var updated = await engine.CompileAsync(documents, CancellationToken.None);
    using (var document = new PdfDocument(updated)) Check(document.Pages.Count == 3, "In-memory included edit");
    Check(await File.ReadAllTextAsync(child) == childText, "Never write unsaved source during compilation");
    documents[child] = "#set text(font: )";
    try { await engine.CompileAsync(documents, CancellationToken.None); throw new Exception("Invalid source exported"); } catch (InvalidOperationException) { }
    for (var i = 0; i < 50 && engine.Problems.Count == 0; i++) await Task.Delay(20);
    Check(engine.Problems.Any(p => p.Path == child && p.Line == 0 && p.Message.Length > 0), "Structured diagnostics locate the erroneous source");
    documents[child] = childText; var recovered = await engine.CompileAsync(documents, CancellationToken.None);
    using (var document = new PdfDocument(recovered)) Check(document.Pages.Count == 2, "Recover after compiler error");
    Console.WriteLine("PASS: unsaved dependencies, compiler errors and recovery");
    documents[main] = string.Join("\n\n", MathSymbols.All.Select(s => "$ " + s.Name + " $"));
    await engine.CompileAsync(documents, CancellationToken.None);
    Console.WriteLine("PASS: every palette symbol compiles");
    Console.WriteLine("ARCHITECTURE: " + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
}
finally { Directory.Delete(root, recursive: true); }
