import Foundation
@main struct LanguageTests {
    static func main() {
        precondition(StudioLanguage.resolved("System", preferred: ["zh-Hans-CN", "en"]) == "zh-Hans")
        precondition(StudioLanguage.resolved("System", preferred: ["zh-CN"]) == "zh-Hans")
        precondition(StudioLanguage.resolved("System", preferred: ["zh-Hant-TW"]) == "en")
        precondition(StudioLanguage.resolved("en", preferred: ["zh-Hans"]) == "en")
        precondition(StudioLanguage.resolved("zh-Hans", preferred: ["en"]) == "zh-Hans")
        precondition(StudioLanguage.text("Settings", language: "zh-Hans") == "设置")
        precondition(StudioLanguage.text("main.typ", language: "zh-Hans") == "main.typ")
        for (key, value) in StudioLanguage.simplifiedChinese {
            precondition(!value.isEmpty)
            precondition(StudioLanguage.text(key, language: "en") == key)
        }
        print("PASS: language override, system fallback, stable English keys and user content")
    }
}
