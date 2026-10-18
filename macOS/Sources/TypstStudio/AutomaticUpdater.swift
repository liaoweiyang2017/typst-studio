import AppKit
import Combine
import Sparkle

/// Sparkle owns networking, verification and the out-of-process installer.
/// This driver only presents non-modal state; it never replaces a running bundle itself.
@MainActor final class AutomaticUpdater: NSObject, ObservableObject, SPUUserDriver, SPUUpdaterDelegate {
    @Published var checkOnStartup = (UserDefaults.standard.object(forKey: "checkUpdatesOnStartup") as? Bool) ?? true {
        didSet { UserDefaults.standard.set(checkOnStartup, forKey: "checkUpdatesOnStartup") }
    }
    @Published private(set) var isChecking = false
    @Published private(set) var status = "Not checked yet."
    @Published private(set) var progress: Double?
    @Published private(set) var readyToRestart = false
    @Published var showBanner = false
    @Published private(set) var details = ""
    let currentVersion = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0.2.2"
    var prepareToRestart: (() -> Bool)?
    private var updater: SPUUpdater!
    private var started = false
    private var checkedOnLaunch = false
    private var received: UInt64 = 0
    private var expected: UInt64 = 0
    private var cancelAction: (() -> Void)?
    private var restartAction: (() -> Void)?
    override init() {
        super.init()
        updater = SPUUpdater(hostBundle: .main, applicationBundle: .main, userDriver: self, delegate: self)
    }
    private func start() -> Bool {
        if started { return true }
        do {
            // We explicitly check on launch; do not add a second scheduled check.
            updater.automaticallyChecksForUpdates = false
            updater.sendsSystemProfile = false
            try updater.start()
            started = true
            return true
        } catch { status = "Update failed"; details = error.localizedDescription; showBanner = true; return false }
    }
    func checkAtLaunch() async {
        guard !checkedOnLaunch else { return }
        checkedOnLaunch = true
        if checkOnStartup { await check(manual: false) }
    }
    func check(manual: Bool = true, presentResult: Bool = true) async {
        guard start(), updater.canCheckForUpdates, !readyToRestart else { return }
        isChecking = true; status = "Checking for updates…"; details = ""; progress = nil
        showBanner = manual && presentResult
        // With our non-modal driver, this begins the download without a blocking dialog.
        updater.checkForUpdates()
    }
    func cancelDownload() { let action = cancelAction; cancelAction = nil; action?() }
    func restart() { guard restartAction != nil, prepareToRestart?() == true else { return }; restartAction?() }
    func displayStatus(language: String) -> String { StudioLanguage.text(status, language: language) }
    func show(_ request: SPUUpdatePermissionRequest, reply: @escaping (SUUpdatePermissionResponse) -> Void) {
        reply(SUUpdatePermissionResponse(automaticUpdateChecks: false, sendSystemProfile: false))
    }
    func showUserInitiatedUpdateCheck(cancellation: @escaping () -> Void) { cancelAction = cancellation }
    func showUpdateFound(with appcastItem: SUAppcastItem, state: SPUUserUpdateState, reply: @escaping (SPUUserUpdateChoice) -> Void) {
        guard !appcastItem.isInformationOnlyUpdate else {
            status = "Update requires manual installation"; isChecking = false; showBanner = true; reply(.dismiss); return
        }
        if state.stage == .installing {
            prepareRestart { reply(.install) }
        } else {
            status = "Downloading update…"; showBanner = true; reply(.install)
        }
    }
    func showUpdateReleaseNotes(with downloadData: SPUDownloadData) {}
    func showUpdateReleaseNotesFailedToDownloadWithError(_ error: Error) {}
    func showUpdateNotFoundWithError(_ error: Error, acknowledgement: @escaping () -> Void) {
        status = "No compatible update available"; details = error.localizedDescription
        isChecking = false; progress = nil; cancelAction = nil; acknowledgement()
    }
    func showUpdaterError(_ error: Error, acknowledgement: @escaping () -> Void) {
        status = "Update failed"; details = error.localizedDescription; isChecking = false
        readyToRestart = false; restartAction = nil; cancelAction = nil; progress = nil; showBanner = true
        acknowledgement()
    }
    func showDownloadInitiated(cancellation: @escaping () -> Void) {
        cancelAction = cancellation; received = 0; expected = 0; progress = nil
        status = "Downloading update…"; isChecking = true; showBanner = true
    }
    func showDownloadDidReceiveExpectedContentLength(_ expectedContentLength: UInt64) { expected = expectedContentLength }
    func showDownloadDidReceiveData(ofLength length: UInt64) {
        received += length
        if expected > 0 { progress = min(1, Double(received) / Double(expected)) }
    }
    func showDownloadDidStartExtractingUpdate() { status = "Preparing update…"; cancelAction = nil; progress = nil }
    func showExtractionReceivedProgress(_ progress: Double) { self.progress = progress }
    private func prepareRestart(_ action: @escaping () -> Void) {
        readyToRestart = true; isChecking = false; progress = nil; cancelAction = nil
        status = "Update ready. Restart to finish installation."; restartAction = action; showBanner = true
    }
    func showReady(toInstallAndRelaunch reply: @escaping (SPUUserUpdateChoice) -> Void) { prepareRestart { reply(.install) } }
    func showInstallingUpdate(withApplicationTerminated applicationTerminated: Bool, retryTerminatingApplication: @escaping () -> Void) {
        status = "Waiting for the application to quit…"
        // If the user cancels the unsaved-document prompt, allow another restart attempt.
        restartAction = applicationTerminated ? nil : retryTerminatingApplication
    }
    func showUpdateInstalledAndRelaunched(_ relaunched: Bool, acknowledgement: @escaping () -> Void) { acknowledgement() }
    func dismissUpdateInstallation() {
        if isChecking { status = "Update cancelled" }
        isChecking = false; cancelAction = nil; progress = nil
    }
    func showUpdateInFocus() { showBanner = true }
    var canCancel: Bool { cancelAction != nil }
}
