import SwiftUI
import AppKit

struct SettingsView: View {
    @ObservedObject var model: StudioModel
    @EnvironmentObject private var updates: AutomaticUpdater
    @State private var category = "General"
    @State private var font = "New Computer Modern"
    // Enumerate fonts once per settings presentation, not on every slider change.
    @State private var fonts = NSFontManager.shared.availableFontFamilies.sorted()
    private let categories = [("General", "gearshape"), ("Editor", "textformat"), ("PDF Preview", "doc.richtext"), ("Software Update", "arrow.triangle.2.circlepath")]

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Text(model.tr("Settings")).font(.headline)
                Spacer()
                Button(model.tr("Done")) { model.showSettings = false }.keyboardShortcut(.defaultAction)
            }.padding(20)
            Divider()
            HStack(spacing: 0) {
                List(selection: $category) {
                    ForEach(categories, id: \.0) { item in
                        Label(model.tr(item.0), systemImage: item.1).tag(item.0)
                    }
                }.listStyle(.sidebar).frame(width: 170)
                Divider()
                Form {
                    switch category {
                    case "Editor": editor
                    case "PDF Preview": preview
                    case "Software Update": softwareUpdate
                    default: general
                    }
                }.formStyle(.grouped).frame(maxWidth: .infinity, maxHeight: .infinity)
            }
        }.frame(width: 760, height: 590)
        .onAppear { if !fonts.contains(font), let first = fonts.first { font = first } }
    }
    private var general: some View {
        Section(model.tr("Language and appearance")) {
            Picker(model.tr("Language"), selection: $model.language) {
                Text(model.tr("Follow system")).tag("System")
                Text("简体中文").tag("zh-Hans")
                Text("English").tag("en")
            }
            Text(model.tr("Language changes apply immediately. System dialogs use the macOS language.")).font(.caption).foregroundStyle(.secondary)
            Picker(model.tr("Appearance"), selection: $model.theme) {
                ForEach(["System", "Light", "Dark"], id: \.self) { Text(model.tr($0)).tag($0) }
            }
        }
    }
    private var editor: some View {
        Group {
            Section(model.tr("Editing")) {
                HStack { Text(model.tr("Editor font size")); Slider(value: $model.fontSize, in: 11...24, step: 1); Text("\(Int(model.fontSize)) pt").monospacedDigit().frame(width: 42) }
                Toggle(model.tr("System spell checking"), isOn: $model.spellCheck)
                Toggle(model.tr("Typing sound"), isOn: $model.typingSound)
            }
            Section(model.tr("Document font")) {
                Picker(model.tr("Font family"), selection: $font) {
                    ForEach(fonts, id: \.self) { Text($0).tag($0) }
                }
                Button(model.tr("Insert font setting")) { model.showSettings = false; model.insert("#set text(font: \"\(font)\")\n") }.disabled(model.showWelcome)
                Text(model.tr("Inserts a Typst font setting into the current document.")).font(.caption).foregroundStyle(.secondary)
            }
        }
    }
    private func palette(_ title: String, selection: Binding<String>, automatic: Bool = false) -> some View {
        Picker(model.tr(title), selection: selection) {
            if automatic { Text(model.tr("Automatic · follow appearance")).tag("Automatic") }
            Text(model.tr("Original colors")).tag("Original")
            Text(model.tr("Night")).tag("Night")
            Text(model.tr("Green reading mode")).tag("Green")
        }
    }
    private var preview: some View {
        Group {
            Section(model.tr("Reading colors")) {
                palette("PDF appearance", selection: $model.pdfAppearance, automatic: true)
                palette("PDF in light appearance", selection: $model.lightPDFAppearance).disabled(model.pdfAppearance != "Automatic")
                palette("PDF in dark appearance", selection: $model.darkPDFAppearance).disabled(model.pdfAppearance != "Automatic")
                Text(model.tr("Preview colors only. Exported PDFs keep their original colors.")).font(.caption).foregroundStyle(.secondary)
            }
            Section(model.tr("Navigation")) {
                Toggle(model.tr("Preview follows cursor (source map)"), isOn: $model.followCursor)
                Text(model.tr("Source maps locate formulas and included files. Use ⇧⌘J from source, ⌥⌘J from selected PDF text, or ⌘-click a PDF glyph. Unrendered code has no page position.")).font(.caption).foregroundStyle(.secondary)
            }
        }
    }
    private var softwareUpdate: some View {
        Section(model.tr("Software Update")) {
            LabeledContent(model.tr("Current version"), value: updates.currentVersion)
            Toggle(model.tr("Check for updates on startup"), isOn: $updates.checkOnStartup)
            Button(model.tr(updates.isChecking ? "Checking…" : "Check for Updates…")) { Task { await updates.check(presentResult: false) } }.disabled(updates.isChecking)
            Text(updates.displayStatus(language: model.language)).font(.caption).foregroundStyle(.secondary).textSelection(.enabled)
            if !updates.details.isEmpty { Text(updates.details).font(.caption).foregroundStyle(.secondary).textSelection(.enabled) }
            if updates.isChecking { ProgressView(value: updates.progress) }
            if updates.readyToRestart { Button(model.tr("Restart to Update")) { updates.restart() } }
            if updates.canCancel { Button(model.tr("Cancel download")) { updates.cancelDownload() } }
            Text(model.tr("Updates download in the background. You can keep editing until you choose to restart.")).font(.caption).foregroundStyle(.secondary)
        }
    }
}
