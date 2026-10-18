import Foundation
@main struct PDFOutputTests {
    static func write(_ writer: PDFOutput, _ data: Data, _ entry: URL) async -> Result<URL, Error> {
        await withCheckedContinuation { continuation in writer.write(data, beside: entry) { continuation.resume(returning: $0) } }
    }
    static func main() async throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let entry = root.appendingPathComponent("main.typ"), target = root.appendingPathComponent("main.pdf")
        let writer = PDFOutput(), bytes = Data("%PDF-1.7\nfixture".utf8)
        try "source".write(to: entry, atomically: true, encoding: .utf8)
        _ = try await write(writer, bytes, entry).get()
        precondition((try? Data(contentsOf: target)) == bytes)
        let modified = try FileManager.default.attributesOfItem(atPath: target.path)[.modificationDate] as! Date
        _ = try await write(writer, bytes, entry).get()
        precondition((try? FileManager.default.attributesOfItem(atPath: target.path)[.modificationDate] as? Date) == modified)
        try FileManager.default.setAttributes([.posixPermissions: 0o444], ofItemAtPath: target.path)
        if case .success = await write(writer, Data("replacement".utf8), entry) { fatalError("Read-only output was overwritten") }
        precondition((try? Data(contentsOf: target)) == bytes)
        try FileManager.default.setAttributes([.posixPermissions: 0o644], ofItemAtPath: target.path)
        try FileManager.default.removeItem(at: target)
        let other = root.appendingPathComponent("other.pdf"); try bytes.write(to: other)
        try FileManager.default.createSymbolicLink(at: target, withDestinationURL: other)
        if case .success = await write(writer, Data("replacement".utf8), entry) { fatalError("Symlink output followed") }
        precondition((try? Data(contentsOf: other)) == bytes)
        precondition((try? String(contentsOf: entry, encoding: .utf8)) == "source")
        print("PASS: same-directory PDF, identical bytes skip IO, readonly/symlink protection and unchanged source")
    }
}
