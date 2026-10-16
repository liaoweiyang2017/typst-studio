import SwiftUI
import AppKit
import PDFKit

@main struct TypstStudioApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var delegate
    @StateObject private var model = StudioModel()
    var body: some Scene {
        Window("Typst Studio", id: "studio") {
            Group {
                if model.showWelcome { WelcomeView(model: model) }
                else { StudioView(model: model) }
            }
                .onAppear { delegate.model = model; NSApp.setActivationPolicy(.regular); NSApp.activate(ignoringOtherApps: true) }
                .background(WindowGuard(model: model))
                .onOpenURL { if ProjectFiles.isDirectory($0) { model.openProject($0) } else { model.openFile($0) } }
        }.defaultSize(width: 1360, height: 900)
        .commands {
            CommandGroup(replacing: .newItem) {
                Button("Welcome") { if model.confirmDiscard() { model.showWelcome = true } }
                Button("New Document") { model.newDocument() }.keyboardShortcut("n")
                Button("Open…") { model.openPanel() }.keyboardShortcut("o")
                Button("Open Project Folder…") { model.openPanel(folder: true) }.keyboardShortcut("o", modifiers: [.command, .shift])
            }
            CommandGroup(replacing: .saveItem) {
                Button("Save") { model.save() }.keyboardShortcut("s")
                Button("Save As…") { model.save(as: true) }.keyboardShortcut("s", modifiers: [.command, .shift])
                Divider()
                Button("Export PDF…") { model.exportPDF() }.keyboardShortcut("e", modifiers: [.command, .shift])
            }
            CommandGroup(replacing: .appSettings) { Button("Settings…") { model.showSettings = true }.keyboardShortcut(",") }
            CommandMenu("Typeset") {
                Button("Compile") { model.compile() }.keyboardShortcut("b")
                Button("Symbols") { model.showSymbols = true }.keyboardShortcut("m", modifiers: [.command, .shift])
                Button("Pop Out Preview") { model.popOut() }
                Toggle("Follow Cursor (Text Match)", isOn: $model.followCursor)
            }
            CommandGroup(after: .textEditing) {
                Button("Find…") {
                    let item = NSMenuItem(); item.tag = NSTextFinder.Action.showFindInterface.rawValue
                    model.editor?.performFindPanelAction(item)
                }.keyboardShortcut("f")
            }
        }
    }
}
final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    weak var model: StudioModel?
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        guard model?.confirmDiscard() ?? true else { return .terminateCancel }
        return .terminateNow
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
    func windowShouldClose(_ sender: NSWindow) -> Bool { model?.confirmDiscard() ?? true }
}
struct StudioView: View {
    @ObservedObject var model: StudioModel
    @State private var section = 0
    @State private var search = ""
    @State private var previewVisible = true
    private let modes = [("folder", "Files"), ("magnifyingglass", "Search"), ("list.bullet.indent", "Outline"), ("x.squareroot", "Symbols"), ("exclamationmark.triangle", "Issues")]
    var body: some View {
        HSplitView {
            if model.sidebar { sidebar.frame(minWidth: 190, idealWidth: 230, maxWidth: 260) }
            VStack(spacing: 0) {
                SourceEditor(model: model)
                if model.showDiagnostics && !model.diagnostics.isEmpty {
                    Divider()
                    HStack(alignment: .top) {
                        Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.orange)
                        ScrollView { Text(model.diagnostics).font(.system(size: 11, design: .monospaced)).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading) }.frame(maxHeight: 70)
                        if let line = model.errorLine { Button("Line \(line)") { model.goToLine(line) } }
                        Button { model.showDiagnostics = false } label: { Image(systemName: "xmark") }
                    }.buttonStyle(.borderless).padding(10)
                }
                Divider()
                HStack(spacing: 16) {
                    formatButton("bold", "Bold", "*", "*")
                    formatButton("italic", "Emphasis", "_", "_")
                    formatButton("underline", "Underline", "#underline[", "]")
                    Divider().frame(height: 12)
                    Button { model.insert("#align(center)[text]") } label: { Image(systemName: "text.aligncenter") }.help("Center")
                    Button { model.insert("\n- ") } label: { Image(systemName: "list.bullet") }.help("Bullet list")
                    Button { model.insert("\n+ ") } label: { Image(systemName: "list.number") }.help("Numbered list")
                    Button { model.showSymbols = true } label: { Image(systemName: "sum") }.help("Symbols")
                    Spacer()
                    if model.compiling { ProgressView().controlSize(.mini) }
                    Text("\(model.text.split { $0.isWhitespace || $0.isNewline }.count) words").foregroundStyle(.secondary)
                }.font(.system(size: 11)).buttonStyle(.borderless).padding(.horizontal, 14).frame(height: 27).background(Color(nsColor: .textBackgroundColor))
            }.frame(minWidth: 350, idealWidth: 590)
            if previewVisible { PreviewPane(model: model).frame(minWidth: 320, idealWidth: 460) }
        }
        .frame(minWidth: 980, minHeight: 620).tint(.blue)
        .preferredColorScheme(model.theme == "Dark" ? .dark : model.theme == "Light" ? .light : nil)
        .toolbar {
            ToolbarItem(placement: .navigation) { Button { model.sidebar.toggle() } label: { Image(systemName: "sidebar.left") }.help("Toggle sidebar") }
            ToolbarItem(placement: .principal) {
                HStack(spacing: 7) { Text(model.title).font(.system(size: 13, weight: .semibold)); if model.dirty { Circle().fill(.secondary).frame(width: 5, height: 5) } }
            }
            ToolbarItemGroup(placement: .primaryAction) {
                Button { section = 4; model.sidebar = true; model.showDiagnostics.toggle() } label: { Image(systemName: model.diagnostics.isEmpty ? "exclamationmark.triangle.fill" : "exclamationmark.triangle.fill").foregroundStyle(model.diagnostics.isEmpty ? Color.secondary : .orange) }.help(model.status)
                Button { previewVisible.toggle() } label: { Image(systemName: "rectangle.on.rectangle").foregroundStyle(previewVisible ? Color.blue : .secondary) }.help("Toggle PDF preview")
            }
        }
        .sheet(isPresented: $model.showSymbols) { SymbolsView(model: model) }
        .sheet(isPresented: $model.showSettings) { SettingsView(model: model) }
        .sheet(isPresented: Binding(get: { model.assetURL != nil }, set: { if !$0 { model.assetURL = nil } })) {
            VStack {
                HStack { Text(model.assetURL?.lastPathComponent ?? "Preview").font(.headline); Spacer(); Button("Done") { model.assetURL = nil } }
                if let url = model.assetURL {
                    if let image = NSImage(contentsOf: url) { Image(nsImage: image).resizable().scaledToFit() }
                    else { ScrollView { Text((try? String(contentsOf: url, encoding: .utf8)) ?? "This file format cannot be previewed.").textSelection(.enabled) } }
                }
            }.padding(20).frame(width: 650, height: 550)
        }
    }
    var sidebar: some View {
        VStack(spacing: 12) {
            HStack(spacing: 0) {
                ForEach(0..<modes.count, id: \.self) { index in
                    Button { section = index } label: {
                        Image(systemName: modes[index].0).font(.system(size: 12)).frame(width: 29, height: 29)
                            .foregroundStyle(section == index ? Color.white : Color.primary)
                            .background(section == index ? Color.blue : Color.clear, in: Capsule())
                    }.buttonStyle(.plain).help(modes[index].1).accessibilityLabel(modes[index].1)
                }
            }.padding(3).background(Color.primary.opacity(0.055), in: Capsule()).padding(.top, 6)
            if section == 0 {
                ScrollView {
                    VStack(spacing: 1) {
                        if let root = model.projectURL { ProjectBranch(model: model, directory: root, depth: 0) }
                        else { fileRow(model.title, active: true, ext: "typ") {} }
                    }.padding(.horizontal, 6)
                }
                HStack {
                    Button { model.openPanel(folder: true) } label: { Image(systemName: "folder.badge.plus") }.help("Open project folder")
                    Button { model.newDocument() } label: { Image(systemName: "doc.badge.plus") }.help("New document")
                    Spacer()
                    Button { model.refreshFiles() } label: { Image(systemName: "arrow.clockwise") }.help("Refresh files")
                    Button { model.showSettings = true } label: { Image(systemName: "gearshape") }.help("Settings")
                }.buttonStyle(.borderless).foregroundStyle(.secondary).padding(12)
            } else if section == 1 {
                ProjectSearchPane(model: model)
            } else if section == 2 {
                ScrollView {
                    if let root = model.pdf?.outlineRoot { PDFOutlineRows(node: root, model: model).padding(.horizontal, 10) }
                    else { VStack(alignment: .leading) { ForEach(model.outline) { item in Button(item.title) { model.goToLine(item.id) }.buttonStyle(.plain).padding(6) } }.padding(10) }
                }
            } else if section == 3 {
                SymbolPalette(model: model)
            } else {
                if model.diagnostics.isEmpty {
                    Spacer()
                    VStack(spacing: 13) { Image(systemName: "checkmark.circle").font(.system(size: 32, weight: .ultraLight)).foregroundStyle(.tertiary); Text("No problems.").font(.system(size: 14)).foregroundStyle(.secondary) }
                    Spacer()
                } else {
                    ScrollView { VStack(alignment: .leading, spacing: 12) {
                        Text(model.diagnostics).font(.system(size: 11, design: .monospaced)).textSelection(.enabled)
                        if let line = model.errorLine { Button("Go to line \(line)") { model.goToLine(line) } }
                        Button("Compile Again") { model.compile() }.disabled(model.compiling)
                    }.padding(14) }
                }
            }
        }.background(Color(nsColor: .windowBackgroundColor))
    }
    func formatButton(_ icon: String, _ title: String, _ prefix: String, _ suffix: String) -> some View {
        Button { model.wrapSelection(prefix, suffix) } label: { Image(systemName: icon) }.help(title)
    }
    func fileRow(_ name: String, active: Bool, ext: String, action: @escaping () -> Void) -> some View {
        ProjectFileRow(name: name, active: active, ext: ext, action: action)
    }
}
struct ProjectFileRow: View {
    let name: String; let active: Bool; let ext: String; var indent: CGFloat = 0; let action: () -> Void
    var body: some View {
        Button(action: action) {
            HStack(spacing: 7) {
                if ext == "typ" { Text("t").font(.system(size: 16, weight: .bold, design: .serif)).foregroundStyle(active ? .white : .teal).frame(width: 16) }
                else if ext == "json" { Text("{}").font(.system(size: 12, design: .monospaced)).fixedSize().foregroundStyle(active ? .white : .orange).frame(width: 18) }
                else { Image(systemName: ext == "pdf" ? "doc.fill" : ["png", "jpg", "jpeg", "svg"].contains(ext) ? "photo" : ["md", "txt", "rst"].contains(ext) ? "doc.text.fill" : ext == "bib" ? "books.vertical.fill" : "doc.fill").foregroundStyle(active ? Color.white : ext == "pdf" ? .red : .gray).frame(width: 16) }
                Text(name).font(.system(size: 12)).lineLimit(1); Spacer(minLength: 0)
            }.padding(.leading, indent + 9).padding(.trailing, 5).frame(height: 25).foregroundStyle(active ? .white : .primary)
                .background(active ? Color.blue : .clear, in: RoundedRectangle(cornerRadius: 6))
        }.buttonStyle(.plain)
    }
}
struct ProjectBranch: View {
    @ObservedObject var model: StudioModel
    let directory: URL; let depth: Int
    @State private var children: [URL] = []
    var body: some View {
        VStack(spacing: 1) {
        ForEach(children, id: \.self) { url in
            if ProjectFiles.isDirectory(url) {
                let expanded = model.expandedDirectories.contains(url.path)
                Button {
                    if expanded { model.expandedDirectories.remove(url.path) } else { model.expandedDirectories.insert(url.path) }
                } label: {
                    HStack(spacing: 5) {
                        Image(systemName: expanded ? "chevron.down" : "chevron.right").font(.system(size: 9, weight: .semibold)).foregroundStyle(.secondary).frame(width: 10)
                        Image(systemName: "folder.fill").foregroundStyle(Color(red: 0.25, green: 0.64, blue: 1))
                        Text(url.lastPathComponent).font(.system(size: 12)); Spacer()
                    }.padding(.leading, CGFloat(depth * 15 + 5)).frame(height: 25).contentShape(Rectangle())
                }.buttonStyle(.plain)
                if expanded { ProjectBranch(model: model, directory: url, depth: depth + 1) }
            } else {
                ProjectFileRow(name: url.lastPathComponent, active: model.fileURL == url, ext: url.pathExtension, indent: CGFloat(depth * 15 + 15), action: { model.openFile(url) })
            }
        }
        }.task(id: model.fileTreeRevision) { children = ProjectFiles.children(directory) }
    }
}
struct PDFOutlineRows: View {
    let node: PDFOutline
    @ObservedObject var model: StudioModel
    var body: some View {
        ForEach(0..<node.numberOfChildren, id: \.self) { i in
            if let child = node.child(at: i) {
                if child.numberOfChildren > 0 {
                    DisclosureGroup { PDFOutlineRows(node: child, model: model) } label: {
                        Button(child.label ?? "Heading") { if let dest = child.destination { model.preview?.go(to: dest) } }.buttonStyle(.plain).font(.system(size: 12, weight: .medium)).lineLimit(1)
                    }.padding(.vertical, 3)
                } else {
                    Button(child.label ?? "Heading") { if let dest = child.destination { model.preview?.go(to: dest) } }.buttonStyle(.plain).font(.system(size: 12)).foregroundStyle(.secondary).lineLimit(1).padding(.vertical, 4).frame(maxWidth: .infinity, alignment: .leading)
                }
            }
        }
    }
}
struct SymbolsView: View {
    @ObservedObject var model: StudioModel
    var body: some View {
        VStack { HStack { Text("Symbols").font(.title2.bold()); Spacer(); Button("Done") { model.showSymbols = false } }.padding(); SymbolPalette(model: model) }.frame(width: 440, height: 560)
    }
}
struct SettingsView: View {
    @ObservedObject var model: StudioModel
    @State var font = "New Computer Modern"
    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            HStack { Text("Editor settings").font(.title2.bold()); Spacer(); Button("Done") { model.showSettings = false } }
            Form {
                Picker("Appearance", selection: $model.theme) { ForEach(["System", "Light", "Dark"], id: \.self) { Text($0) } }
                HStack { Text("Editor font size"); Slider(value: $model.fontSize, in: 11...24, step: 1); Text("\(Int(model.fontSize)) pt") }
                Toggle("System spell checking", isOn: $model.spellCheck)
                Toggle("Typing sound", isOn: $model.typingSound)
                Toggle("Preview follows cursor (text matching)", isOn: $model.followCursor)
                Picker("Document font", selection: $font) { ForEach(NSFontManager.shared.availableFontFamilies.sorted(), id: \.self) { Text($0) } }
                Button("Insert font setting") { model.showSettings = false; model.insert("#set text(font: \"\(font)\")\n") }
            }.formStyle(.grouped)
            Text("⌘-click a PDF text line to find its source. Matching is text based; formulas and repeated text may not map precisely.").font(.caption).foregroundStyle(.secondary)
        }.padding(24).frame(width: 520, height: 440)
        .onChange(of: model.fontSize) { _, value in UserDefaults.standard.set(value, forKey: "fontSize") }
        .onChange(of: model.theme) { _, value in UserDefaults.standard.set(value, forKey: "theme") }
    }
}

struct WindowGuard: NSViewRepresentable {
    let model: StudioModel
    func makeCoordinator() -> Guard { Guard(model) }
    func makeNSView(context: Context) -> NSView {
        let view = NSView()
        DispatchQueue.main.async { view.window?.delegate = context.coordinator; view.window?.titleVisibility = .hidden }
        return view
    }
    func updateNSView(_ view: NSView, context: Context) {}
    final class Guard: NSObject, NSWindowDelegate {
        let model: StudioModel
        init(_ model: StudioModel) { self.model = model }
        func windowShouldClose(_ sender: NSWindow) -> Bool {
            let close = model.confirmDiscard()
            if close { model.dirty = false }
            return close
        }
    }
}
