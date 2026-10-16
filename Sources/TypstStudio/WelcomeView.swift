import SwiftUI
import AppKit

struct WelcomeView: View {
    @ObservedObject var model: StudioModel
    @State private var templates = false
    @State private var openingRecent: String?
    var body: some View {
        GeometryReader { geometry in
        HStack(spacing: 0) {
            VStack(spacing: 0) {
                Spacer(minLength: 20)
                if let icon = NSImage(contentsOf: Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/AppIcon.icns")) {
                    Image(nsImage: icon).resizable().scaledToFit().frame(width: 132, height: 132)
                }
                Text("Typst Studio").font(.system(size: 42, weight: .bold)).padding(.top, 18)
                Text("Version 0.1.4").font(.system(size: 16)).foregroundStyle(.secondary).padding(.top, 5)
                Spacer().frame(height: 38)
                VStack(spacing: 13) {
                    welcomeButton("Open Folder…", icon: "folder") { model.openPanel(folder: true) }
                    welcomeButton("New Project from Template…", icon: "doc.badge.plus") { templates = true }
                    welcomeButton("Open File…", icon: "doc.text") { model.openPanel() }
                }.frame(maxWidth: 410)
                Text("Opening a file opens its folder as the project.").font(.system(size: 12)).foregroundStyle(.tertiary).padding(.top, 18)
                if model.hasRecovery { Button("Restore Unsaved Draft") { model.restoreDraft() }.buttonStyle(.link).padding(.top, 12) }
                Spacer(minLength: 25)
            }.padding(.horizontal, 32).frame(maxWidth: .infinity)
            VStack(alignment: .leading, spacing: 20) {
                Text("Recent Projects").font(.system(size: 21, weight: .bold)).padding(.top, 25)
                if model.recentProjects.isEmpty {
                    Spacer(); Text("No recent projects").font(.system(size: 17)).foregroundStyle(.tertiary).frame(maxWidth: .infinity); Spacer()
                } else {
                    ScrollView {
                        VStack(alignment: .leading, spacing: 10) {
                            ForEach(model.recentProjects, id: \.self) { path in
                                Button {
                                    openingRecent = path
                                    Task { @MainActor in
                                        // A short feedback frame makes the selected row visible before navigation.
                                        try? await Task.sleep(nanoseconds: 70_000_000)
                                        let exists = await Task.detached { FileManager.default.fileExists(atPath: path) }.value
                                        if exists { model.openProject(URL(fileURLWithPath: path)) }
                                        else { model.report("This project is no longer at its saved location. Use Open Folder to locate it.") }
                                        openingRecent = nil
                                    }
                                } label: {
                                    HStack(spacing: 12) { Image(systemName: "folder").foregroundStyle(.blue); VStack(alignment: .leading, spacing: 4) { Text(URL(fileURLWithPath: path).lastPathComponent).font(.system(size: 14, weight: .medium)); Text(path.replacingOccurrences(of: NSHomeDirectory(), with: "~")).font(.caption).foregroundStyle(.secondary).lineLimit(2) }; Spacer(); if openingRecent == path { ProgressView().controlSize(.small) } }.padding(.horizontal, 10).padding(.vertical, 10).frame(maxWidth: .infinity, alignment: .leading).contentShape(Rectangle())
                                }.buttonStyle(RecentProjectStyle(selected: openingRecent == path)).disabled(openingRecent != nil)
                            }
                        }
                    }
                }
            }.padding(.horizontal, 24).frame(width: geometry.size.width * 0.37).background(Color(nsColor: .windowBackgroundColor).opacity(0.6))
        }
        }.frame(minWidth: 900, minHeight: 610).background(Color(nsColor: .textBackgroundColor))
        .sheet(isPresented: $templates) {
            VStack(alignment: .leading, spacing: 18) {
                HStack { Text("New Project from Template").font(.title2.bold()); Spacer(); Button("Cancel") { templates = false } }
                ForEach(0..<3) { index in
                    Button(["Blank document", "Article", "Mathematics notes"][index]) {
                        templates = false
                        model.createTemplate(index)
                    }.buttonStyle(.bordered).controlSize(.large)
                }
            }.padding(25).frame(width: 490)
        }
    }
    func welcomeButton(_ title: String, icon: String, action: @escaping () -> Void) -> some View {
        Button(action: action) { HStack(spacing: 18) { Image(systemName: icon).font(.system(size: 23)).foregroundStyle(.blue).frame(width: 28); Text(title).font(.system(size: 18)); Spacer() }.padding(.horizontal, 23).frame(height: 57).background(Color.primary.opacity(0.045), in: RoundedRectangle(cornerRadius: 13)) }.buttonStyle(.plain)
    }
}

struct RecentProjectStyle: ButtonStyle {
    let selected: Bool
    func makeBody(configuration: Configuration) -> some View {
        configuration.label.background(Color.primary.opacity(selected || configuration.isPressed ? 0.10 : 0), in: RoundedRectangle(cornerRadius: 8))
    }
}
