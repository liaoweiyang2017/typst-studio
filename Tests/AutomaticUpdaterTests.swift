import Foundation
import Sparkle
@main struct AutomaticUpdaterTests {
    @MainActor static func main() {
        let updater = AutomaticUpdater()
        var cancelled = false, installed = false
        updater.showDownloadInitiated { cancelled = true }
        updater.showDownloadDidReceiveExpectedContentLength(100)
        updater.showDownloadDidReceiveData(ofLength: 25)
        precondition(updater.isChecking && updater.progress == 0.25 && updater.canCancel)
        updater.cancelDownload(); precondition(cancelled)
        updater.dismissUpdateInstallation()
        precondition(!updater.isChecking && updater.progress == nil)
        updater.showDownloadDidStartExtractingUpdate()
        updater.showReady(toInstallAndRelaunch: { installed = $0 == .install })
        precondition(updater.readyToRestart && !updater.isChecking && !installed)
        updater.prepareToRestart = { false }; updater.restart()
        precondition(!installed, "Canceling the save prompt must not signal the installer")
        updater.prepareToRestart = { true }; updater.restart()
        precondition(installed)
        updater.showUpdaterError(NSError(domain: "test", code: 1)) {}
        precondition(!updater.readyToRestart && !updater.isChecking && updater.showBanner)
        print("PASS: background progress, cancellation, explicit restart gate, save-cancel protection and failure state reset")
    }
}
