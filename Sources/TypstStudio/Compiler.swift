import Foundation
struct CompileResult { var pdf: Data?; var diagnostics: String; var elapsed: Double; var success: Bool }
final class Compiler {
    static let executable: URL = {
        let bundled = Bundle.main.bundleURL.appendingPathComponent("Contents/Helpers/typst")
        return FileManager.default.isExecutableFile(atPath: bundled.path) ? bundled : URL(fileURLWithPath: "/opt/homebrew/bin/typst")
    }()
    static func compile(_ text: String, directory: URL, root: URL) -> CompileResult {
        let start = Date(), temp = FileManager.default.temporaryDirectory.appendingPathComponent("typst-studio-\(UUID().uuidString)")
        do {
            try FileManager.default.createDirectory(at: temp, withIntermediateDirectories: true)
            defer { try? FileManager.default.removeItem(at: temp) }
            let output = temp.appendingPathComponent("preview.pdf"), input = temp.appendingPathComponent("input"), errors = temp.appendingPathComponent("errors")
            try Data(text.utf8).write(to: input); FileManager.default.createFile(atPath: errors.path, contents: nil)
            let ih = try FileHandle(forReadingFrom: input), eh = try FileHandle(forWritingTo: errors)
            defer { try? ih.close(); try? eh.close() }
            let p = Process(); p.executableURL = executable; p.currentDirectoryURL = directory
            p.arguments = ["compile", "--root", root.path, "--diagnostic-format", "short", "-", output.path]
            p.standardInput = ih; p.standardError = eh; p.standardOutput = FileHandle.nullDevice
            try p.run()
            let deadline = Date().addingTimeInterval(30)
            while p.isRunning && Date() < deadline { Thread.sleep(forTimeInterval: 0.025) }
            let timeout = p.isRunning
            if timeout { p.terminate() }; p.waitUntilExit()
            let data = try? Data(contentsOf: output)
            return CompileResult(pdf: data, diagnostics: (timeout ? "Compilation exceeded 30 seconds.\n" : "") + ((try? String(contentsOf: errors, encoding: .utf8)) ?? ""), elapsed: Date().timeIntervalSince(start), success: !timeout && p.terminationStatus == 0 && data != nil)
        } catch { return CompileResult(pdf: nil, diagnostics: error.localizedDescription, elapsed: Date().timeIntervalSince(start), success: false) }
    }
}
