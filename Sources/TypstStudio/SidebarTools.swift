import SwiftUI
import Foundation

struct SearchHit: Identifiable {
    let url: URL; let line: Int; let text: String
    var id: String { url.path + ":" + String(line) }
}
enum ProjectSearch {
    static func find(_ query: String, caseSensitive: Bool, files: [URL], currentURL: URL?, currentText: String) -> [SearchHit] {
        guard !query.isEmpty else { return [] }
        let extensions = Set(["typ", "md", "txt", "json", "bib", "yaml", "yml", "toml", "py", "rst", "tex", "csv", "bat", "sh"])
        var hits: [SearchHit] = []
        for url in files where extensions.contains(url.pathExtension) || url.lastPathComponent == "Makefile" {
            if ((try? url.resourceValues(forKeys: [.fileSizeKey]).fileSize) ?? 0) > 1_000_000 { continue }
            let content = url == currentURL ? currentText : ((try? String(contentsOf: url, encoding: .utf8)) ?? "")
            for (i, line) in content.components(separatedBy: "\n").enumerated() {
                if line.range(of: query, options: caseSensitive ? [] : [.caseInsensitive]) != nil {
                    hits.append(SearchHit(url: url, line: i + 1, text: line.trimmingCharacters(in: .whitespaces)))
                    if hits.count == 200 { return hits }
                }
            }
        }
        return hits
    }
}
struct ProjectSearchPane: View {
    @ObservedObject var model: StudioModel
    @State private var query = ""
    @State private var caseSensitive = false
    @State private var hits: [SearchHit] = []
    @State private var searching = false
    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                Text("Find"); Image(systemName: "chevron.right").font(.caption2)
                Menu(caseSensitive ? "Matching Case" : "Containing") {
                    Button("Containing") { caseSensitive = false }
                    Button("Matching Case") { caseSensitive = true }
                }.menuStyle(.borderlessButton).fixedSize()
            }.font(.system(size: 11)).foregroundStyle(.secondary).padding(.horizontal, 13)
            HStack(spacing: 6) { Image(systemName: "magnifyingglass").foregroundStyle(.secondary); TextField("Search", text: $query).textFieldStyle(.plain) }.padding(8).background(Color.primary.opacity(0.07), in: RoundedRectangle(cornerRadius: 7)).padding(.horizontal, 9)
            if query.isEmpty {
                Spacer(); empty("Search the project", icon: "magnifyingglass"); Spacer()
            } else if searching { Spacer(); ProgressView().frame(maxWidth: .infinity); Spacer() }
            else if hits.isEmpty { Spacer(); empty("No matches", icon: "magnifyingglass"); Spacer() }
            else {
                Text(hits.count == 200 ? "First 200 matches" : "\(hits.count) matches").font(.caption).foregroundStyle(.secondary).padding(.horizontal, 13)
                ScrollView { LazyVStack(alignment: .leading, spacing: 10) {
                    ForEach(hits) { hit in
                        Button {
                            if hit.url != model.fileURL { model.openFile(hit.url) }
                            if hit.url == model.fileURL { model.goToLine(hit.line) }
                        } label: {
                            VStack(alignment: .leading, spacing: 4) {
                                Text("\(hit.url.lastPathComponent):\(hit.line)").font(.system(size: 11, weight: .medium)).foregroundStyle(.blue)
                                Text(hit.text).font(.system(size: 11)).lineLimit(3)
                            }.frame(maxWidth: .infinity, alignment: .leading)
                        }.buttonStyle(.plain)
                        Divider()
                    }
                }.padding(12) }
            }
        }
        .task(id: query + "\(caseSensitive)\(model.revision)\(model.files.count)") {
            guard !query.isEmpty else { hits = []; searching = false; return }
            searching = true
            do { try await Task.sleep(nanoseconds: 200_000_000) } catch { return }
            let text = model.text, files = model.files, url = model.fileURL, q = query, cs = caseSensitive
            let result = await Task.detached(priority: .userInitiated) { ProjectSearch.find(q, caseSensitive: cs, files: files, currentURL: url, currentText: text) }.value
            guard !Task.isCancelled else { return }
            hits = result; searching = false
        }
    }
    func empty(_ text: String, icon: String) -> some View {
        VStack(spacing: 14) { Image(systemName: icon).font(.system(size: 33, weight: .ultraLight)).foregroundStyle(.tertiary); Text(text).font(.system(size: 14)).foregroundStyle(.secondary) }.frame(maxWidth: .infinity)
    }
}
struct SymbolPalette: View {
    @ObservedObject var model: StudioModel
    @State private var query = ""
    static let symbols: [(String, String)] = [
        ("𝔸","AA"),("𝔹","BB"),("ℂ","CC"),("𝔻","DD"),("𝔼","EE"),("𝔽","FF"),("ℍ","HH"),("ℕ","NN"),("ℙ","PP"),("ℚ","QQ"),("ℝ","RR"),("ℤ","ZZ"),
        ("Α","Alpha"),("Β","Beta"),("Γ","Gamma"),("Δ","Delta"),("Θ","Theta"),("Λ","Lambda"),("Ξ","Xi"),("Π","Pi"),("Σ","Sigma"),("Φ","Phi"),("Ψ","Psi"),("Ω","Omega"),
        ("α","alpha"),("β","beta"),("γ","gamma"),("δ","delta"),("ε","epsilon"),("ζ","zeta"),("η","eta"),("θ","theta"),("ι","iota"),("κ","kappa"),("λ","lambda"),("μ","mu"),("ν","nu"),("ξ","xi"),("π","pi"),("ρ","rho"),("σ","sigma"),("τ","tau"),("υ","upsilon"),("φ","phi"),("χ","chi"),("ψ","psi"),("ω","omega"),
        ("∫","integral"),("∬","integral.double"),("∮","integral.cont"),("∑","sum"),("∏","product"),("∞","infinity"),("∂","partial"),("∇","nabla"),("√","sqrt(x)"),("±","plus.minus"),("×","times"),("÷","div"),("≤","lt.eq"),("≥","gt.eq"),("≠","eq.not"),("≈","approx"),("≡","equiv"),("∈","in"),("∉","in.not"),("⊂","subset"),("⊆","subset.eq"),("∪","union"),("∩","inter"),("∅","nothing"),("∀","forall"),("∃","exists"),("¬","not"),("∧","and"),("∨","or"),("→","arrow.r"),("←","arrow.l"),("↔","arrow.l.r"),("⇒","arrow.r.double"),("⇔","arrow.l.r.double"),("∴","therefore"),("∵","because")
    ]
    var body: some View {
        VStack(spacing: 10) {
            HStack { Image(systemName: "magnifyingglass").foregroundStyle(.secondary); TextField("Search symbols", text: $query).textFieldStyle(.plain) }.padding(8).background(Color.primary.opacity(0.07), in: RoundedRectangle(cornerRadius: 7)).padding(.horizontal, 8)
            ScrollView { LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 7), count: 4), spacing: 7) {
                ForEach(Self.symbols.filter { query.isEmpty || $0.1.localizedCaseInsensitiveContains(query) || $0.0.contains(query) }, id: \.1) { glyph, name in
                    Button {
                        let location = model.editor?.selectedRange().location ?? 0
                        let prefix = (model.text as NSString).substring(to: min(location, (model.text as NSString).length))
                        model.insert(prefix.filter { $0 == "$" }.count % 2 == 1 ? name : "$ " + name + " $")
                    } label: { Text(glyph).font(.system(size: 27, design: .serif)).frame(maxWidth: .infinity).frame(height: 48).background(Color(nsColor: .textBackgroundColor).opacity(0.65), in: RoundedRectangle(cornerRadius: 7)).overlay(RoundedRectangle(cornerRadius: 7).strokeBorder(Color.primary.opacity(0.13))) }.buttonStyle(.plain).help(name)
                }
            }.padding(9) }
        }
    }
}
