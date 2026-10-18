import SwiftUI
import AppKit
import PDFKit
import UniformTypeIdentifiers

struct OutlineItem: Identifiable { let id: Int; let title: String }
@MainActor private final class DocumentSession {
    let compiler = TinymistCompiler()
    var source = ""
    var revision = -1
    var pdf: PDFDocument?
    var entryURL: URL?
    var diagnostics = ""
    var success = false
    var elapsed = 0.0
    var previewPosition: PreviewPosition?
}
@MainActor final class StudioModel: ObservableObject {
    @Published var text = ""
    @Published var showWelcome = true
    @Published var recentProjects: [String] = UserDefaults.standard.stringArray(forKey: "recentProjects") ?? []
    @Published var hasRecovery = false
    @Published var indexing = false
    @Published var expandedDirectories: Set<String> = []
    @Published var fileTreeRevision = 0
    @Published var fileURL: URL?
    @Published var projectURL: URL?
    @Published var files: [URL] = []
    @Published var pdf: PDFDocument?
    @Published var diagnostics = ""
    @Published var pdfOutputError = ""
    private let pdfOutput = PDFOutput()
    @Published var compiling = false
    @Published var elapsed = 0.0
    @Published var dirty = false
    @Published var line = 1
    @Published var column = 1
    @Published var jumpLine: Int?
    @Published var jumpRange: NSRange?
    @Published var navigationStatus = ""
    private var pinnedSessionKey: String?
    private var displayedSessionKey: String?
    private var displayedEntryURL: URL?
    private var navigationSerial = UUID()
    private var navigationDebounce: DispatchWorkItem?
    @Published var sidebar = true
    @Published var showSymbols = false
    @Published var showDiagnostics = false
    @Published var showSettings = false
    @Published var assetURL: URL?
    @Published var revision = 0
    @Published var successfulRevision = -1
    @Published var status = "Preparing preview…"
    @Published var fontSize = 14.0 { didSet { UserDefaults.standard.set(fontSize, forKey: "fontSize") } }
    @Published var theme = "System" { didSet { UserDefaults.standard.set(theme, forKey: "theme") } }
    @Published var language = UserDefaults.standard.string(forKey: "language") ?? "System" { didSet { UserDefaults.standard.set(language, forKey: "language") } }
    func tr(_ key: String) -> String { StudioLanguage.text(key, language: language) }
    @Published var pdfAppearance = UserDefaults.standard.string(forKey: "pdfAppearance") ?? "Automatic" {
        didSet { UserDefaults.standard.set(pdfAppearance, forKey: "pdfAppearance") }
    }
    @Published var lightPDFAppearance = UserDefaults.standard.string(forKey: "lightPDFAppearance") ?? "Original" {
        didSet { UserDefaults.standard.set(lightPDFAppearance, forKey: "lightPDFAppearance") }
    }
    @Published var darkPDFAppearance = UserDefaults.standard.string(forKey: "darkPDFAppearance") ?? "Night" {
        didSet { UserDefaults.standard.set(darkPDFAppearance, forKey: "darkPDFAppearance") }
    }
    @Published var spellCheck = (UserDefaults.standard.object(forKey: "spellCheck") as? Bool) ?? true { didSet { UserDefaults.standard.set(spellCheck, forKey: "spellCheck") } }
    @Published var typingSound = (UserDefaults.standard.object(forKey: "typingSound") as? Bool) ?? false { didSet { UserDefaults.standard.set(typingSound, forKey: "typingSound") } }
    @Published var followCursor = (UserDefaults.standard.object(forKey: "followCursor") as? Bool) ?? false { didSet { UserDefaults.standard.set(followCursor, forKey: "followCursor") } }
    weak var editor: NSTextView?
    weak var preview: PDFView?
    private var debounce: DispatchWorkItem?
    private var sessions: [String: DocumentSession] = [:]
    private var sessionOrder: [String] = []
    private let recoveryQueue = DispatchQueue(label: "studio.recovery", qos: .utility)
    private var previewWindow: NSWindow?
    private var lastSavedText = ""
    private let recoveryURL: URL
    private let persistsRecentProjects: Bool
    private var pendingRecovery: Recovery?
    struct Recovery: Codable { let text: String; let path: String?; let project: String?; var dirty: Bool? = nil }
    init(recoveryLocation: URL? = nil) {
        let bundleID = Bundle.main.bundleIdentifier ?? "app.typststudio.mac"
        let supportName = bundleID == "app.typststudio.mac" ? "TypstStudio" : bundleID
        let dir = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent(supportName)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        persistsRecentProjects = recoveryLocation == nil
        recoveryURL = recoveryLocation ?? dir.appendingPathComponent("Recovery.json")
        let defaults = UserDefaults.standard
        fontSize = defaults.double(forKey: "fontSize") == 0 ? 13 : defaults.double(forKey: "fontSize")
        theme = defaults.string(forKey: "theme") ?? "System"
        if let data = try? Data(contentsOf: recoveryURL), let r = try? JSONDecoder().decode(Recovery.self, from: data) {
            pendingRecovery = r
            // Showing Welcome must not open a possibly remote/unavailable document.
            hasRecovery = !r.text.isEmpty && (r.dirty ?? true)
        }
    }
    func restoreDraft() {
        guard let r = pendingRecovery else { return }
        text = r.text; fileURL = r.path.map { URL(fileURLWithPath: $0) }
        projectURL = r.project.map { URL(fileURLWithPath: $0) }
        if let projectURL, !FileManager.default.fileExists(atPath: projectURL.path) { self.projectURL = fileURL?.deletingLastPathComponent() }
        lastSavedText = fileURL.flatMap { try? String(contentsOf: $0, encoding: .utf8) } ?? ""
        dirty = text != lastSavedText; showWelcome = false; hasRecovery = false
        refreshFiles(); expandToFile(); revision += 1; scheduleCompile()
    }
    func rememberProject(_ url: URL) {
        recentProjects.removeAll { $0 == url.path }; recentProjects.insert(url.path, at: 0)
        recentProjects = Array(recentProjects.prefix(12))
        if persistsRecentProjects { UserDefaults.standard.set(recentProjects, forKey: "recentProjects") }
    }
    func expandToFile() {
        guard let fileURL, let projectURL else { return }
        var parent = fileURL.deletingLastPathComponent()
        while parent.path.hasPrefix(projectURL.path + "/") {
            expandedDirectories.insert(parent.path); parent.deleteLastPathComponent()
        }
    }
    var title: String { fileURL?.lastPathComponent ?? "Untitled.typ" }
    var outline: [OutlineItem] {
        text.components(separatedBy: "\n").enumerated().compactMap { i, line in
            let s = line.trimmingCharacters(in: .whitespaces)
            guard s.hasPrefix("="), let space = s.firstIndex(of: " ") else { return nil }
            return OutlineItem(id: i + 1, title: String(s[s.index(after: space)...]))
        }
    }
    var errorLine: Int? {
        let path = NSRegularExpression.escapedPattern(for: fileURL?.path ?? (projectURL ?? FileManager.default.temporaryDirectory).appendingPathComponent(".typst-studio-untitled.typ").path)
        guard let re = try? NSRegularExpression(pattern: "(?:" + path + #"|<stdin>|-):(\d+):(\d+)"#), let m = re.firstMatch(in: diagnostics, range: NSRange(diagnostics.startIndex..., in: diagnostics)), let r = Range(m.range(at: 1), in: diagnostics) else { return nil }
        return Int(diagnostics[r])
    }
    func changed(_ value: String) { navigationSerial = UUID(); navigationStatus = ""; text = value; dirty = text != lastSavedText; revision += 1; scheduleCompile() }
    func persistRecovery() {
        let value = Recovery(text: text, path: fileURL?.path, project: projectURL?.path, dirty: dirty), url = recoveryURL
        recoveryQueue.async { if let data = try? JSONEncoder().encode(value) { try? data.write(to: url, options: .atomic) } }
    }
    func scheduleCompile() {
        debounce?.cancel()
        if pinnedSessionKey == nil { pinnedSessionKey = documentKey; session(for: activeSessionKey).entryURL = fileURL }
        let key = activeSessionKey, sourceFile = fileURL, source = text, version = revision
        let work = DispatchWorkItem { [weak self] in
            guard let self, self.activeSessionKey == key else { return }
            self.debounce = nil; self.persistRecovery()
            self.compilePreview(file: sourceFile, source: source, version: version)
        }
        debounce = work
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.12, execute: work)
    }
    private var activeSessionKey: String { pinnedSessionKey ?? documentKey }
    var previewDocumentKey: String { displayedSessionKey ?? activeSessionKey }
    var pinnedPreviewName: String? { displayedEntryURL?.lastPathComponent }
    var compileTargetURL: URL? { sessions[activeSessionKey]?.entryURL }
    var compileButtonHelp: String { tr("Compile current file") + ": " + title + " (⌘B)" }
    func rememberPreview(_ position: PreviewPosition, for key: String) { sessions[key]?.previewPosition = position }
    func previewPosition(for key: String) -> PreviewPosition? { sessions[key]?.previewPosition }
    private func capturePreview() {
        guard let view = preview as? StudioPDFView, let key = view.documentKey, let position = view.readingPosition() else { return }
        rememberPreview(position, for: key)
    }
    private var documentKey: String { (projectURL?.path ?? "") + "|" + (fileURL?.path ?? "untitled") }
    private func session(for key: String) -> DocumentSession {
        sessionOrder.removeAll { $0 == key }; sessionOrder.append(key)
        if let existing = sessions[key] { return existing }
        let created = DocumentSession(); sessions[key] = created
        while sessionOrder.count > 3 {
            let oldest = sessionOrder.removeFirst()
            if let removed = sessions.removeValue(forKey: oldest) {
                let engine = removed.compiler
                DispatchQueue.global(qos: .utility).async { engine.shutdown() }
            }
        }
        return created
    }
    func shutdownCompiler() {
        debounce?.cancel(); navigationDebounce?.cancel(); navigationSerial = UUID()
        for session in sessions.values { session.compiler.shutdown() }
        sessions.removeAll(); sessionOrder.removeAll(); pinnedSessionKey = nil; displayedSessionKey = nil; displayedEntryURL = nil
    }
    /// Explicit command: the visible editor becomes the preview entry.
    func compile() {
        guard !showWelcome else { return }
        debounce?.cancel(); debounce = nil; capturePreview()
        navigationSerial = UUID(); navigationDebounce?.cancel()
        let target = documentKey
        pinnedSessionKey = target
        let session = session(for: target); session.entryURL = fileURL
        revision += 1; successfulRevision = -1
        if let cached = session.pdf { pdf = cached; displayedSessionKey = target; displayedEntryURL = session.entryURL }
        compilePreview(file: fileURL, source: text, version: revision)
    }
    /// Automatic edits update the fixed entry; browsing never calls this method.
    private func compilePreview(file sourceFile: URL?, source: String, version: Int) {
        guard !showWelcome else { return }
        compiling = true; status = pdf == nil ? "Typesetting…" : "Updating preview…"
        let key = activeSessionKey, session = session(for: activeSessionKey)
        session.revision = version
        let directory = session.entryURL?.deletingLastPathComponent() ?? projectURL ?? FileManager.default.temporaryDirectory
        let completion: (Int, CompileResult) -> Void = { [weak self, weak session] version, result in
            let document = result.pdf.flatMap { PDFDocument(data: $0) }
            DispatchQueue.main.async {
                guard let self, let session, self.sessions[key] === session, session.revision == version else { return }
                if let document, !result.fromCache || session.pdf == nil { session.pdf = document }
                session.diagnostics = result.diagnostics; session.success = result.success; session.elapsed = result.elapsed
                guard self.activeSessionKey == key, self.revision == version, !self.showWelcome else { return }
                self.compiling = false; self.elapsed = session.elapsed; self.diagnostics = session.diagnostics
                if session.success, let pdf = session.pdf {
                    self.pdf = pdf; self.displayedSessionKey = key; self.displayedEntryURL = session.entryURL
                    self.successfulRevision = version; self.status = "Preview up to date"
                    if let entry = session.entryURL, let bytes = result.pdf {
                        self.pdfOutput.write(bytes, beside: entry) { [weak self] outcome in
                            DispatchQueue.main.async {
                                guard let self, self.activeSessionKey == key, self.revision == version else { return }
                                if case .failure(let error) = outcome { if !(error is CancellationError) { self.pdfOutputError = error.localizedDescription } }
                                else { self.pdfOutputError = "" }
                            }
                        }
                    }
                } else { self.successfulRevision = -1; self.status = "Compilation failed · last successful preview"; self.showDiagnostics = true }
            }
        }
        if let sourceFile, sourceFile != session.entryURL {
            session.compiler.updateIncludedFile(sourceFile, source: source, revision: version, completion: completion)
        } else {
            session.source = source
            session.compiler.submit(.init(source: source, directory: directory, root: projectURL ?? directory,
                document: session.entryURL?.path ?? "untitled", revision: version, completion: completion))
        }
    }
    func finishPDFWrites(_ completion: @escaping @MainActor () -> Void) {
        pdfOutput.flush { DispatchQueue.main.async { completion() } }
    }
    private var updateRestartApproved = false
    func prepareForUpdateRestart() -> Bool {
        guard confirmDiscard() else { return false }
        updateRestartApproved = true
        return true
    }
    func confirmTermination() -> Bool { updateRestartApproved || confirmDiscard() }
    func confirmDiscard() -> Bool {
        guard dirty else { return true }
        let a = NSAlert(); a.messageText = "Save changes to \(title)?"; a.informativeText = "Your document has unsaved changes."
        a.addButton(withTitle: "Save"); a.addButton(withTitle: "Cancel"); a.addButton(withTitle: "Discard")
        switch a.runModal() { case .alertFirstButtonReturn: return save(); case .alertThirdButtonReturn: return true; default: return false }
    }
    func createTemplate(_ index: Int) {
        guard confirmDiscard() else { return }
        let panel = NSSavePanel(); panel.nameFieldStringValue = "main.typ"
        panel.allowedContentTypes = [UTType(filenameExtension: "typ") ?? .plainText]
        guard panel.runModal() == .OK, let url = panel.url else { return }
        let templates = ["= Untitled\n\nStart writing here.\n", StudioModel.sample, "#set page(paper: \"a4\")\n#set heading(numbering: \"1.\")\n= Mathematics notes\n\n$ integral_0^1 x dif x = 1/2 $\n"]
        do { try templates[index].write(to: url, atomically: true, encoding: .utf8); dirty = false; fileURL = nil; openFile(url); if compileTargetURL != url { compile() } }
        catch { report(error.localizedDescription) }
    }
    func newDocument() {
        guard confirmDiscard() else { return }; if let pinnedSessionKey { sessions[pinnedSessionKey]?.compiler.closeIncludedFiles() }; pinnedSessionKey = nil; showWelcome = false; fileURL = nil; text = "= Untitled\n\nStart writing here.\n"; lastSavedText = ""; dirty = true; revision += 1
        editor?.undoManager?.removeAllActions(); persistRecovery(); scheduleCompile()
    }
    func openPanel(folder: Bool = false) {
        let p = NSOpenPanel(); p.canChooseDirectories = folder; p.canChooseFiles = !folder; p.allowsMultipleSelection = false
        if !folder { p.allowedContentTypes = [UTType(filenameExtension: "typ") ?? .plainText] }
        if p.runModal() == .OK, let url = p.url {
            if folder { openProject(url) }
            else { openFile(url) }
        }
    }
    func openProject(_ url: URL) {
        let url = url.standardizedFileURL.resolvingSymlinksInPath()
        guard confirmDiscard() else { return }
        shutdownCompiler()
        projectURL = url; files = []; fileURL = nil; text = ""; lastSavedText = ""; dirty = false
        pdf = nil; diagnostics = ""; revision += 1; successfulRevision = -1
        showWelcome = false; expandedDirectories = []; rememberProject(url)
        refreshFiles(selectMain: true)
    }
    func openFile(_ url: URL, preservingPreview: Bool = false) {
        let url = url.standardizedFileURL.resolvingSymlinksInPath()
        if url.pathExtension != "typ" { assetURL = url; return }
        guard fileURL != url, confirmDiscard() else { return }
        capturePreview()
        navigationSerial = UUID(); navigationDebounce?.cancel(); jumpRange = nil; navigationStatus = ""
        do {
            let content = try String(contentsOf: url, encoding: .utf8)
            // A discarded edit must not remain in the compiler's in-memory include.
            if dirty { text = fileURL.flatMap { try? String(contentsOf: $0, encoding: .utf8) } ?? lastSavedText; dirty = false; revision += 1; debounce?.cancel(); debounce = nil; compilePreview(file: fileURL, source: text, version: revision) }
            if let pending = debounce { pending.perform(); pending.cancel(); debounce = nil }
            if projectURL == nil || !url.path.hasPrefix(projectURL!.path + "/") {
                shutdownCompiler(); projectURL = url.deletingLastPathComponent(); pdf = nil; successfulRevision = -1
            }
            showWelcome = false; fileURL = url; text = content; jumpLine = 1; lastSavedText = content; dirty = false
            editor?.undoManager?.removeAllActions(); expandToFile(); if let projectURL { rememberProject(projectURL) }; if files.isEmpty { refreshFiles() }; persistRecovery()
            if pinnedSessionKey == nil { compile() }
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
            if destination != fileURL {
                if let projectURL, !destination.path.hasPrefix(projectURL.path + "/") { shutdownCompiler() }
                pinnedSessionKey = nil
            }
            fileURL = destination; lastSavedText = text; dirty = false
            if projectURL == nil || !destination.path.hasPrefix(projectURL!.path + "/") { projectURL = destination.deletingLastPathComponent() }
            refreshFiles(); persistRecovery(); revision += 1; scheduleCompile(); return true
        } catch { report(error.localizedDescription); return false }
    }
    func refreshFiles(selectMain: Bool = false) {
        guard let projectURL else { files = []; return }
        indexing = true; fileTreeRevision += 1
        DispatchQueue.global(qos: .userInitiated).async { [weak self] in
            let found = ProjectFiles.index(projectURL)
            DispatchQueue.main.async { [weak self] in
                guard let self, self.projectURL == projectURL else { return }
                self.files = found; self.indexing = false
                if selectMain && self.fileURL == nil {
                    if let main = ProjectFiles.mainFile(in: found, root: projectURL) { self.openFile(main) }
                    else { self.status = "No Typst source found. Create a document or open a .typ file." }
                }
            }
        }
    }
    func exportPDF() {
        guard successfulRevision == revision, let pdf else { report("Wait for a successful compile of the current document before exporting."); return }
        let p = NSSavePanel(); p.allowedContentTypes = [.pdf]; p.nameFieldStringValue = ((pinnedPreviewName ?? title) as NSString).deletingPathExtension + ".pdf"
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
        w.contentView = NSHostingView(rootView: PreviewPane(model: self, primary: false)); previewWindow = w; w.makeKeyAndOrderFront(nil)
    }
    func report(_ s: String) { let a = NSAlert(); a.messageText = s; a.runModal() }
    func selectionChanged(_ range: NSRange) {
        let prefix = (text as NSString).substring(to: min(range.location, (text as NSString).length))
        let newLine = prefix.components(separatedBy: "\n").count
        let newColumn = (prefix.components(separatedBy: "\n").last?.count ?? 0) + 1
        if line != newLine { line = newLine }
        if column != newColumn { column = newColumn }
        if followCursor {
            navigationDebounce?.cancel()
            let work = DispatchWorkItem { [weak self] in self?.jumpToPDF(quiet: true) }
            navigationDebounce = work
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.18, execute: work)
        }
    }
    func jumpToPDF(quiet: Bool = false) {
        guard let session = sessions[activeSessionKey], pdf != nil, successfulRevision == revision, !compiling else {
            if !quiet { navigationStatus = "Wait for compilation to finish, then try again." }; return
        }
        let range = editor?.selectedRange() ?? NSRange(location: 0, length: 0)
        let position = SourceCoordinates.position(in: text, utf16Offset: range.location)
        let file = fileURL ?? (projectURL ?? FileManager.default.temporaryDirectory).appendingPathComponent(".typst-studio-untitled.typ")
        let key = activeSessionKey, version = revision, serial = UUID(); navigationSerial = serial
        if !quiet { navigationStatus = "Finding PDF location…" }
        session.compiler.forward(file: file, line: position.line, column: position.column) { [weak self] result in
            DispatchQueue.main.async {
                guard let self, self.navigationSerial == serial, self.revision == version, let document = self.pdf, self.activeSessionKey == key else { return }
                if case .failure(let error) = result { if !quiet { self.navigationStatus = error.localizedDescription }; return }
                guard case .success(.document(let positions)) = result, !positions.isEmpty else {
                    if !quiet { self.navigationStatus = "No rendered position for this source." }; return
                }
                let currentPage = self.preview?.currentPage.flatMap { document.index(for: $0) } ?? 0
                guard let position = positions.min(by: { abs($0.page - 1 - currentPage) < abs($1.page - 1 - currentPage) }), let page = document.page(at: position.page - 1) else { return }
                let bounds = page.bounds(for: .mediaBox)
                let point = CGPoint(x: bounds.minX + position.x, y: bounds.maxY - position.y)
                self.preview?.go(to: CGRect(x: point.x - 12, y: point.y - 12, width: 24, height: 24), on: page)
                (self.preview as? StudioPDFView)?.highlightSourcePoint(point, on: page)
                self.navigationStatus = "PDF location found"
            }
        }
    }
    func jumpFromPDF(page: PDFPage, point: CGPoint) {
        guard let session = sessions[activeSessionKey], let pdf, successfulRevision == revision, !compiling else { navigationStatus = "Wait for compilation to finish, then try again."; return }
        let pageIndex = pdf.index(for: page)
        guard pageIndex != NSNotFound else { return }
        let bounds = page.bounds(for: .mediaBox), version = revision, serial = UUID()
        navigationSerial = serial; navigationStatus = "Finding source location…"
        session.compiler.inverse(page: pageIndex + 1, x: point.x - bounds.minX, y: bounds.maxY - point.y) { [weak self] result in
            DispatchQueue.main.async {
                guard let self, self.navigationSerial == serial, self.revision == version else { return }
                if case .failure(let error) = result { self.navigationStatus = error.localizedDescription; return }
                guard case .success(.source(let location)) = result else { return }
                let target = location.url.standardizedFileURL
                // Package sources can be navigated to, but never silently leave the project.
                guard let root = self.projectURL, target.path.hasPrefix(root.path + "/") || target == self.fileURL else {
                    self.navigationStatus = "Source belongs to an external package."; return
                }
                if target != self.fileURL {
                    self.openFile(target, preservingPreview: true)
                    guard self.fileURL == target else { return }
                }
                guard let range = SourceCoordinates.range(in: self.text, start: location.start, end: location.end) else { self.navigationStatus = "Source changed. Try navigating again."; return }
                self.jumpLine = nil; self.jumpRange = range; self.navigationStatus = "Source location found"
            }
        }
    }
    func jumpFromSelectedPDF() {
        guard let view = preview as? StudioPDFView else { return }
        view.showSelectedSource()
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
