using Microsoft.UI.Xaml.Media.Imaging;

namespace TypstStudio.App;

internal static class FileIcons
{
    private static SvgImageSource Image(string name) => new(new Uri($"ms-appx:///Assets/FileIcons/{name}.svg"));
    private static readonly SvgImageSource Default = Image("document");
    private static readonly SvgImageSource Folder = Image("folder"), OpenFolder = Image("folder-open");
    private static readonly Dictionary<string, SvgImageSource> Types = new(StringComparer.OrdinalIgnoreCase) {
        [".typ"] = Image("typ"), [".pdf"] = Image("pdf"), [".json"] = Image("json"), [".md"] = Image("markdown"),
        [".png"] = Image("image"), [".jpg"] = Image("image"), [".jpeg"] = Image("image"), [".svg"] = Image("image"),
        [".bat"] = Image("terminal"), [".cmd"] = Image("terminal"), [".ps1"] = Image("terminal")
    };
    public static SvgImageSource For(string path, bool folder, bool expanded) => folder ? (expanded ? OpenFolder : Folder) : Types.GetValueOrDefault(Path.GetExtension(path), Default);
}
