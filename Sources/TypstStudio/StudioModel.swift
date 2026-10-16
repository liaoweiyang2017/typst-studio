import SwiftUI
import AppKit
import PDFKit
import UniformTypeIdentifiers

struct OutlineItem: Identifiable { let id: Int; let title: String }
@MainActor final class StudioModel: ObservableObject {
    @Published var text = StudioModel.sample
    @Published var fileURL: URL?
    @Published var projectURL: URL?
    @Published var files: [URL] = []
    @Published var pdf: PDFDocument?
    @Published var diagnostics = ""
    @Published var compiling = false
    @Published var elapsed = 0.0
    @Published var dirty = false
    @Published var line = 1
    @Published var column = 1
    @Published var jumpLine: Int?
    @Published var sidebar = true
    @Published var showSymbols = false
    @Published var showDiagnostics = false
    @Published var showSettings = false
    @Published var assetURL: URL?
    @Published var revision = 0
    @Published var successfulRevision = -1
    @Published var status = "Preparing preview…"
    @Published var fontSize = 14.0
    @Published var theme = "System"
    @Published var spellCheck = true
    @Published var typingSound = false
    @Published var followCursor = false
    weak var editor: NSTextView?
    weak var preview: PDFView?
    private var debounce: DispatchWorkItem?
    private let queue = DispatchQueue(label: "studio.compiler", qos: .userInitiated)
    private var activeCompile = false
    private var previewWindow: NSWindow?
    private var lastSavedText = ""
    private let recoveryURL: URL = {
        let dir = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("TypstStudio")
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir.appendingPathComponent("Recovery.json")
    }()
    struct Recovery: Codable { let text: String; let path: String?; let project: String? }
    init() {
        let defaults = UserDefaults.standard
        fontSize = defaults.double(forKey: "fontSize") == 0 ? 14 : defaults.double(forKey: "fontSize")
        theme = defaults.string(forKey: "theme") ?? "System"
        if let data = try? Data(contentsOf: recoveryURL), let r = try? JSONDecoder().decode(Recovery.self, from: data) {
            text = r.text; fileURL = r.path.map { URL(fileURLWithPath: $0) }; projectURL = r.project.map { URL(fileURLWithPath: $0) }
            lastSavedText = fileURL.flatMap { try? String(contentsOf: $0, encoding: .utf8) } ?? ""
            dirty = text != lastSavedText; refreshFiles()
        }
        scheduleCompile()
    }
    var title: String { fileURL?.lastPathComponent ?? "Welcome.typ" }
    var outline: [OutlineItem] {
        text.components(separatedBy: "\n").enumerated().compactMap { i, line in
            let s = line.trimmingCharacters(in: .whitespaces)
            guard s.hasPrefix("="), let space = s.firstIndex(of: " ") else { return nil }
            return OutlineItem(id: i + 1, title: String(s[s.index(after: space)...]))
        }
    }
    var errorLine: Int? {
        guard let re = try? NSRegularExpression(pattern: #"(?:<stdin>|-):(\d+):(\d+):"#), let m = re.firstMatch(in: diagnostics, range: NSRange(diagnostics.startIndex..., in: diagnostics)), let r = Range(m.range(at: 1), in: diagnostics) else { return nil }
        return Int(diagnostics[r])
    }
    func changed(_ value: String) { text = value; dirty = text != lastSavedText; revision += 1; persistRecovery(); scheduleCompile() }
    func persistRecovery() {
        if let data = try? JSONEncoder().encode(Recovery(text: text, path: fileURL?.path, project: projectURL?.path)) { try? data.write(to: recoveryURL, options: .atomic) }
    }
    func scheduleCompile() {
        debounce?.cancel(); let work = DispatchWorkItem { [weak self] in self?.compile() }; debounce = work
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.35, execute: work)
    }
    func compile() {
        guard !activeCompile else { return }; activeCompile = true; compiling = true; status = "Typesetting…"
        let source = text, version = revision
        let directory = fileURL?.deletingLastPathComponent() ?? projectURL ?? FileManager.default.temporaryDirectory
        let root = projectURL ?? directory
        queue.async {
            let result = Compiler.compile(source, directory: directory, root: root)
            DispatchQueue.main.async { [weak self] in
                guard let self else { return }; self.activeCompile = false
                guard self.revision == version else { self.compile(); return }
                self.compiling = false; self.elapsed = result.elapsed; self.diagnostics = result.diagnostics
                if result.success, let data = result.pdf, let doc = PDFDocument(data: data) {
                    self.pdf = doc; self.successfulRevision = version; self.status = "Preview up to date"
                } else { self.status = "Compilation failed · last successful preview"; self.showDiagnostics = true }
            }
        }
    }
    func confirmDiscard() -> Bool {
        guard dirty else { return true }
        let a = NSAlert(); a.messageText = "Save changes to \(title)?"; a.informativeText = "Your document has unsaved changes."
        a.addButton(withTitle: "Save"); a.addButton(withTitle: "Cancel"); a.addButton(withTitle: "Discard")
        switch a.runModal() { case .alertFirstButtonReturn: return save(); case .alertThirdButtonReturn: return true; default: return false }
    }
    func newDocument() {
        guard confirmDiscard() else { return }; fileURL = nil; text = "= Untitled\n\nStart writing here.\n"; lastSavedText = ""; dirty = true; revision += 1
        editor?.undoManager?.removeAllActions(); persistRecovery(); scheduleCompile()
    }
    func openPanel(folder: Bool = false) {
        let p = NSOpenPanel(); p.canChooseDirectories = folder; p.canChooseFiles = !folder; p.allowsMultipleSelection = false
        if !folder { p.allowedContentTypes = [UTType(filenameExtension: "typ") ?? .plainText] }
        if p.runModal() == .OK, let url = p.url {
            if folder { guard confirmDiscard() else { return }; projectURL = url; refreshFiles(); persistRecovery() }
            else { openFile(url) }
        }
    }
    func openFile(_ url: URL) {
        if url.pathExtension != "typ" { assetURL = url; return }
        guard fileURL != url, confirmDiscard() else { return }
        do {
            let content = try String(contentsOf: url, encoding: .utf8)
            fileURL = url; text = content; lastSavedText = content; dirty = false
            if projectURL == nil || !url.path.hasPrefix(projectURL!.path + "/") { projectURL = url.deletingLastPathComponent() }
            revision += 1; pdf = nil; editor?.undoManager?.removeAllActions(); refreshFiles(); persistRecovery(); scheduleCompile()
        } catch { report(error.localizedDescription) }
    }
    @discardableResult func save(as: Bool = false) -> Bool {
        var destination = fileURL
        if destination == nil || `as` {
            let p = NSSavePanel(); p.nameFieldStringValue = title; p.allowedContentTypes = [UTType(filenameExtension: "typ") ?? .plainText]
            guard p.runModal() == .OK, let url = p.url else { return false }; destination = url
        }
        guard let destination else { return false }
        do {
            if destination == fileURL, let disk = try? String(contentsOf: destination, encoding: .utf8), disk != lastSavedText {
                let a = NSAlert(); a.messageText = "This file changed on disk"; a.informativeText = "Replace the disk version with your current edits?"; a.addButton(withTitle: "Replace"); a.addButton(withTitle: "Cancel")
                guard a.runModal() == .alertFirstButtonReturn else { return false }
            }
            try text.write(to: destination, atomically: true, encoding: .utf8)
            fileURL = destination; lastSavedText = text; dirty = false
            if projectURL == nil || !destination.path.hasPrefix(projectURL!.path + "/") { projectURL = destination.deletingLastPathComponent() }
            refreshFiles(); persistRecovery(); revision += 1; scheduleCompile(); return true
        } catch { report(error.localizedDescription); return false }
    }
    func refreshFiles() {
        guard let projectURL else { files = []; return }
        let e = FileManager.default.enumerator(at: projectURL, includingPropertiesForKeys: nil, options: [.skipsHiddenFiles, .skipsPackageDescendants])
        var found: [URL] = []
        while let url = e?.nextObject() as? URL {
            if ["typ", "png", "jpg", "jpeg", "svg", "pdf", "bib", "yaml", "json"].contains(url.pathExtension.lowercased()) { found.append(url) }
            if found.count >= 1000 { break }
        }
        files = found.sorted { $0.path < $1.path }
    }
    func exportPDF() {
        guard successfulRevision == revision, let pdf else { report("Wait for a successful compile of the current document before exporting."); return }
        let p = NSSavePanel(); p.allowedContentTypes = [.pdf]; p.nameFieldStringValue = (title as NSString).deletingPathExtension + ".pdf"
        if p.runModal() == .OK, let url = p.url, !pdf.write(to: url) { report("Could not write the PDF.") }
    }
    func wrapSelection(_ prefix: String, _ suffix: String) {
        guard let editor else { return }
        let range = editor.selectedRange()
        let selected = (editor.string as NSString).substring(with: range)
        insert(prefix + (selected.isEmpty ? "text" : selected) + suffix)
    }
    func insert(_ s: String) { editor?.window?.makeFirstResponder(editor); editor?.insertText(s, replacementRange: editor?.selectedRange() ?? NSRange(location: 0, length: 0)) }
    func goToLine(_ n: Int) { jumpLine = n }
    func popOut() {
        if let previewWindow { previewWindow.makeKeyAndOrderFront(nil); return }
        let w = NSWindow(contentRect: NSRect(x: 150, y: 150, width: 650, height: 800), styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        w.title = "Typst Studio — Live Preview"; w.isReleasedWhenClosed = false
        w.contentView = NSHostingView(rootView: PreviewPane(model: self)); previewWindow = w; w.makeKeyAndOrderFront(nil)
    }
    func report(_ s: String) { let a = NSAlert(); a.messageText = s; a.runModal() }
    func selectionChanged(_ range: NSRange) {
        let prefix = (text as NSString).substring(to: min(range.location, (text as NSString).length))
        line = prefix.components(separatedBy: "\n").count; column = (prefix.components(separatedBy: "\n").last?.count ?? 0) + 1
        if followCursor, let pdf {
            let lines = text.components(separatedBy: "\n"), query = text.components(separatedBy: "\n")[min(line - 1, text.components(separatedBy: "\n").count - 1)].trimmingCharacters(in: CharacterSet(charactersIn: "= *#_ "))
            _ = lines
            if query.count > 4, let selection = pdf.findString(query, withOptions: .caseInsensitive).first { preview?.go(to: selection) }
        }
    }
    func jumpFromPDF(_ phrase: String) {
        let needle = phrase.trimmingCharacters(in: .whitespacesAndNewlines)
        guard needle.count > 2, let r = text.range(of: needle, options: .caseInsensitive) else { return }
        goToLine(text[..<r.lowerBound].components(separatedBy: "\n").count)
    }
    static let sample = """
    #set page(paper: "a4", margin: (x: 24mm, y: 24mm))
    #set text(font: "New Computer Modern", size: 11pt)
    #set par(justify: true, leading: 0.75em)
    #set heading(numbering: "1.")

    #align(right)[
      #text(fill: rgb("167d8d"), weight: "bold")[TYPST / STUDIO]
    ]
    #v(20mm)
    #text(size: 32pt, weight: "bold")[A place for\\
    beautiful ideas.]
    #v(4mm)
    #text(size: 13pt, fill: rgb("64748b"))[
      A native workspace for thoughtful typesetting.
    ]
    #v(12mm)

    = Start with an idea

    Great documents begin with clear thinking. Typst Studio brings
    your words, equations, and references into one quiet workspace.
    Edit this source and watch the page take shape beside you.

    = Mathematics, beautifully expressed

    From a simple identity to a new discovery, let the notation
    do the talking.

    $ integral_0^infinity e^(-x^2) dif x = sqrt(pi) / 2 $

    = A little structure goes a long way

    #table(
      columns: (1fr, 2fr), inset: 10pt,
      fill: (x, y) => if y == 0 { rgb("e8f3f3") },
      stroke: 0.5pt + rgb("d4dddd"),
      [*Your workspace*], [*Made for your flow*],
      [Write], [Native editing and syntax colors],
      [Preview], [Live typesetting with local fonts],
      [Publish], [A beautiful PDF, ready to share],
    )

    #v(8mm)
    #text(size: 9pt, fill: rgb("64748b"))[
      Made with Typst Studio · Your ideas, beautifully set.
    ]
    """
}
