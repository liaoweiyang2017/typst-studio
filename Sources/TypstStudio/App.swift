import SwiftUI
import AppKit
import PDFKit

@main struct TypstStudioApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var delegate
    @StateObject private var model = StudioModel()
    @StateObject private var updates = AutomaticUpdater()
    var body: some Scene {
        Window("Typst Studio", id: "studio") {
            Group {
                if model.showWelcome { WelcomeView(model: model) }
                else { StudioView(model: model) }
            }
                .onAppear { delegate.model = model; updates.prepareToRestart = { [weak model = model] in model?.prepareForUpdateRestart() ?? false }; NSApp.setActivationPolicy(.regular); NSApp.activate(ignoringOtherApps: true); Task { await updates.checkAtLaunch() } }
                .sheet(isPresented: $model.showSettings) { SettingsView(model: model).environmentObject(updates) }
                .safeAreaInset(edge: .bottom) {
                    if updates.showBanner { UpdateBanner(model: model, updates: updates) }
                }
                .preferredColorScheme(model.theme == "Dark" ? .dark : model.theme == "Light" ? .light : nil)
                .background(WindowGuard(model: model))
                .onOpenURL { if ProjectFiles.isDirectory($0) { model.openProject($0) } else { model.openFile($0) } }
        }.defaultSize(width: 1360, height: 900)
        .commands {
            CommandGroup(replacing: .newItem) {
                Button(model.tr("Welcome")) { if model.confirmDiscard() { model.showWelcome = true } }
                Button(model.tr("New Document")) { model.newDocument() }.keyboardShortcut("n")
                Button(model.tr("Open…")) { model.openPanel() }.keyboardShortcut("o")
                Button(model.tr("Open Project Folder…")) { model.openPanel(folder: true) }.keyboardShortcut("o", modifiers: [.command, .shift])
            }
            CommandGroup(replacing: .saveItem) {
                Button(model.tr("Save")) { model.save() }.keyboardShortcut("s")
                Button(model.tr("Save As…")) { model.save(as: true) }.keyboardShortcut("s", modifiers: [.command, .shift])
                Divider()
                Button(model.tr("Export PDF…")) { model.exportPDF() }.keyboardShortcut("e", modifiers: [.command, .shift])
            }
            CommandGroup(after: .appInfo) {
                Button(model.tr("Check for Updates…")) { Task { await updates.check() } }.disabled(updates.isChecking)
            }
            CommandGroup(replacing: .appSettings) { Button(model.tr("Settings…")) { model.showSettings = true }.keyboardShortcut(",") }
            CommandMenu(model.tr("Typeset")) {
                Button(model.tr("Show in PDF")) { model.jumpToPDF() }.keyboardShortcut("j", modifiers: [.command, .shift])
                Button(model.tr("Show in Source")) { model.jumpFromSelectedPDF() }.keyboardShortcut("j", modifiers: [.command, .option])
                Button(model.tr("Compile")) { model.compile() }.keyboardShortcut("b")
                Button(model.tr("Symbols")) { model.showSymbols = true }.keyboardShortcut("m", modifiers: [.command, .shift])
                Button(model.tr("Pop Out Preview")) { model.popOut() }
                Toggle(model.tr("Follow Cursor (Source Map)"), isOn: $model.followCursor)
            }
            CommandGroup(after: .textEditing) {
                Button(model.tr("Find…")) {
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
        guard model?.confirmTermination() ?? true else { return .terminateCancel }
        model?.shutdownCompiler()
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
    @State private var sidebarWidth: CGFloat = 230
    var body: some View {
        HSplitView {
            if model.sidebar {
                sidebar.frame(minWidth: 190, idealWidth: 230, maxWidth: 260)
                    .background(GeometryReader { geometry in
                        Color.clear.onAppear { sidebarWidth = geometry.size.width }
                            .onChange(of: geometry.size.width) { sidebarWidth = $0 }
                    })
            }
            if previewVisible {
                EditorPreviewSplit { editorPane } preview: { PreviewPane(model: model) }
            } else { editorPane }

        }
        .frame(minWidth: 980, minHeight: 620).tint(.blue)
        .preferredColorScheme(model.theme == "Dark" ? .dark : model.theme == "Light" ? .light : nil)
        .toolbar {
            if #available(macOS 26.0, *) { sidebarToolbarItem.sharedBackgroundVisibility(.hidden) }
            else { sidebarToolbarItem }
            ToolbarItem(placement: .principal) {
                Text(model.projectURL?.lastPathComponent ?? "Typst Studio").font(.system(size: 13, weight: .semibold)).lineLimit(1)
                    .help(model.projectURL?.path ?? "Typst Studio")
            }
            ToolbarItemGroup(placement: .primaryAction) {
                Button { section = 4; model.sidebar = true; model.showDiagnostics.toggle() } label: { Image(systemName: model.diagnostics.isEmpty ? "exclamationmark.triangle.fill" : "exclamationmark.triangle.fill").foregroundStyle(model.diagnostics.isEmpty ? Color.secondary : .orange) }.help(model.status)
                Button { previewVisible.toggle() } label: { Image(systemName: "rectangle.on.rectangle").foregroundStyle(previewVisible ? Color.blue : .secondary) }.help(model.tr("Toggle PDF preview"))
            }
        }
        .sheet(isPresented: $model.showSymbols) { SymbolsView(model: model) }
        .sheet(isPresented: Binding(get: { model.assetURL != nil }, set: { if !$0 { model.assetURL = nil } })) {
            VStack {
                HStack { Text(model.assetURL?.lastPathComponent ?? "Preview").font(.headline); Spacer(); Button(model.tr("Done")) { model.assetURL = nil } }
                if let url = model.assetURL {
                    if let image = NSImage(contentsOf: url) { Image(nsImage: image).resizable().scaledToFit() }
                    else { ScrollView { Text((try? String(contentsOf: url, encoding: .utf8)) ?? "This file format cannot be previewed.").textSelection(.enabled) } }
                }
            }.padding(20).frame(width: 650, height: 550)
        }
    }
    private var sidebarToolbarItem: some ToolbarContent {
            ToolbarItem(placement: .navigation) {
                HStack {
                    Spacer(minLength: 0)
                    Button { model.sidebar.toggle() } label: { Image(systemName: "sidebar.left") }
                        .buttonStyle(.borderless).frame(width: 32, height: 32).help(model.tr("Toggle sidebar"))
                }.frame(width: model.sidebar ? max(32, sidebarWidth - 100) : 32)
            }
    }
    var editorPane: some View {
            VStack(spacing: 0) {
                HStack(spacing: 8) {
                    Text(model.title).font(.system(size: 13, weight: .semibold)).lineLimit(1).truncationMode(.middle)
                    if model.dirty { Circle().fill(.secondary).frame(width: 4, height: 4) }
                    Spacer(minLength: 12)
                    if model.compiling { ProgressView().controlSize(.mini).help(model.tr("Compiling…")) }
                    Button { model.compile() } label: { Label(model.tr("Compile"), systemImage: "play.fill").labelStyle(.titleAndIcon) }
                        .buttonStyle(.bordered).controlSize(.small).help(model.compileButtonHelp).disabled(model.compiling)
                }.padding(.horizontal, 12).frame(height: 36).background(Color(nsColor: .windowBackgroundColor))
                Divider()
                SourceEditor(model: model).clipped()
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
                    Button { model.showSymbols = true } label: { Image(systemName: "sum") }.help(model.tr("Symbols"))
                    Spacer()
                    Text("\(model.text.split { $0.isWhitespace || $0.isNewline }.count) words").foregroundStyle(.secondary)
                }.font(.system(size: 11)).buttonStyle(.borderless).padding(.horizontal, 14).frame(height: 27).background(Color(nsColor: .textBackgroundColor))
            }
    }
    var sidebar: some View {
        VStack(spacing: 12) {
            SidebarModePicker(selection: $section).frame(width: 166, height: 38).padding(.top, 6)
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
                    Button { model.showSettings = true } label: { Image(systemName: "gearshape") }.help(model.tr("Settings"))
                }.buttonStyle(.borderless).foregroundStyle(.secondary).padding(12)
            } else if section == 1 {
                ProjectSearchPane(model: model)
            } else if section == 2 {
                ScrollView {
                    if let root = model.pdf?.outlineRoot { PDFOutlineRows(node: root, model: model).id(model.fileURL?.path ?? "untitled").padding(.horizontal, 10) }
                    else { VStack(alignment: .leading) { ForEach(model.outline) { item in Button(item.title) { model.goToLine(item.id) }.buttonStyle(.plain).padding(6) } }.padding(10) }
                }
            } else if section == 3 {
                SymbolPalette(model: model)
            } else {
                if model.diagnostics.isEmpty {
                    Spacer()
                    VStack(spacing: 13) { Image(systemName: "checkmark.circle").font(.system(size: 32, weight: .ultraLight)).foregroundStyle(.tertiary); Text(model.tr("No problems.")).font(.system(size: 14)).foregroundStyle(.secondary) }
                    Spacer()
                } else {
                    ScrollView { VStack(alignment: .leading, spacing: 12) {
                        Text(model.diagnostics).font(.system(size: 11, design: .monospaced)).textSelection(.enabled)
                        if let line = model.errorLine { Button("Go to line \(line)") { model.goToLine(line) } }
                        Button("Compile Again") { model.compile() }.disabled(model.compiling)
                    }.padding(14) }
                }
            }
        }.frame(maxWidth: .infinity, maxHeight: .infinity).background(Color(nsColor: .windowBackgroundColor))
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
    struct Entry: Identifiable { let url: URL; let folder: Bool; var id: String { url.path } }
    @State private var children: [Entry] = []
    var body: some View {
        VStack(spacing: 1) {
        ForEach(children) { entry in
            let url = entry.url
            if entry.folder {
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
        }.task(id: directory.path + String(model.fileTreeRevision)) {
            let directory = directory
            let entries = await Task.detached(priority: .userInitiated) {
                ProjectFiles.children(directory).map { Entry(url: $0, folder: ProjectFiles.isDirectory($0)) }
            }.value
            if !Task.isCancelled { children = entries }
        }
    }
}
struct PDFOutlineRows: View {
    let node: PDFOutline
    let model: StudioModel
    var depth = 0
    var body: some View {
        LazyVStack(alignment: .leading, spacing: 0) {
            ForEach(0..<node.numberOfChildren, id: \.self) { i in
                if let child = node.child(at: i) { PDFOutlineRow(node: child, model: model, depth: depth) }
            }
        }.frame(maxWidth: .infinity, alignment: .leading)
    }
}
struct PDFOutlineRow: View {
    let node: PDFOutline
    let model: StudioModel
    let depth: Int
    @State private var expanded = true
    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: 3) {
                if node.numberOfChildren > 0 {
                    Button { expanded.toggle() } label: {
                        Image(systemName: expanded ? "chevron.down" : "chevron.right").font(.system(size: 10, weight: .semibold))
                            .frame(width: 17, height: 27).contentShape(Rectangle())
                    }.buttonStyle(.plain).accessibilityLabel("Toggle " + (node.label ?? "heading"))
                        .accessibilityValue(expanded ? "Expanded" : "Collapsed")
                } else { Color.clear.frame(width: 17, height: 27) }
                Button(node.label ?? "Heading") { if let dest = node.destination { model.preview?.go(to: dest) } }
                    .buttonStyle(.plain).font(.system(size: depth == 0 ? 13 : 12, weight: depth == 0 ? .bold : .regular))
                    .lineLimit(1).help(node.label ?? "").frame(maxWidth: .infinity, alignment: .leading)
                    .contentShape(Rectangle())
            }.foregroundStyle(depth == 0 ? Color.primary : .secondary)
                .padding(.leading, CGFloat(depth * 17)).frame(height: depth == 0 ? 31 : 27)
                .overlay(alignment: .leading) {
                    if depth > 0 {
                        HStack(spacing: 16) {
                            ForEach(0..<depth, id: \.self) { _ in Rectangle().fill(Color.primary.opacity(0.10)).frame(width: 1) }
                        }.padding(.leading, 8).allowsHitTesting(false)
                    }
                }
            if expanded && node.numberOfChildren > 0 { PDFOutlineRows(node: node, model: model, depth: depth + 1) }
        }
    }
}
struct SymbolsView: View {
    @ObservedObject var model: StudioModel
    var body: some View {
        VStack { HStack { Text(model.tr("Symbols")).font(.title2.bold()); Spacer(); Button(model.tr("Done")) { model.showSymbols = false } }.padding(); SymbolPalette(model: model) }.frame(width: 440, height: 560)
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
            if close { model.dirty = false; model.shutdownCompiler() }
            return close
        }
    }
}
