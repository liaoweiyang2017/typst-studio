import SwiftUI
struct UpdateBanner: View {
    @ObservedObject var model: StudioModel
    @ObservedObject var updates: AutomaticUpdater
    var body: some View {
        HStack(spacing: 12) {
            Image(systemName: "arrow.down.circle")
            Text(updates.displayStatus(language: model.language)).font(.callout)
            if updates.isChecking { ProgressView(value: updates.progress).frame(width: 100) }
            Spacer()
            if updates.readyToRestart { Button(model.tr("Restart to Update")) { updates.restart() } }
            if updates.canCancel { Button(model.tr("Cancel download")) { updates.cancelDownload() } }
            Button(model.tr("Settings…")) { model.showSettings = true }
            Button { updates.showBanner = false } label: { Image(systemName: "xmark") }.help(model.tr("Continue working"))
        }.padding(12).background(.regularMaterial)
    }
}
