import Foundation
import PDFKit
@main struct Smoke {
    static func main() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("studio-test-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        try "Included content".write(to: root.appendingPathComponent("part.typ"), atomically: true, encoding: .utf8)
        let result = Compiler.compile("= Hello\n\n#include \"part.typ\"\n\n$ integral_0^1 x dif x = 1/2 $", directory: root, root: root)
        precondition(result.success, "Valid document failed: \(result.diagnostics)")
        let pdf = PDFDocument(data: result.pdf!)!
        precondition(pdf.pageCount == 1)
        precondition(pdf.string?.contains("Included content") == true, "Relative include was not resolved")
        let invalid = Compiler.compile("#set text(font: )", directory: root, root: root)
        precondition(!invalid.success && invalid.pdf == nil)
        precondition(invalid.diagnostics.contains("error:"), "Missing diagnostic")
        let rootRestricted = Compiler.compile("#read(\"/../../etc/passwd\")", directory: root, root: root)
        precondition(!rootRestricted.success, "Compiler root was not enforced")
        print("PASS: PDF generation, relative include, math, diagnostics, project root isolation")
        print("Error format: \(invalid.diagnostics)")
    }
}
