# Typst Studio

**面向 macOS 与 Windows 的原生 Typst 编辑器。** 在同一工作区编辑源码、编译 PDF，并在文字、公式与跨文件源码之间定位。

[English](README.md) · [下载安装包](https://github.com/liaoweiyang2017/typst-studio/releases/latest) · [版本说明](docs/Release-0.2.2.md) · [参与贡献](CONTRIBUTING.md)

## 特色功能

- **各平台原生界面**：Mac 使用 SwiftUI、AppKit、PDFKit；Windows 使用 WinUI 3 与 Fluent 控件，不使用网页编辑器。
- **持续编译**：内置 Tinymist，保留编译进程与缓存，编辑包含文件时更新完整主文档。
- **明确的编译入口**：普通文件切换和 PDF 反向定位不改变预览；点击“编译”才将当前编辑文件设为入口。
- **同目录 PDF 输出**：成功编译后，`main.typ` 旁自动生成或更新 `main.pdf`；自动编译也会更新，失败保留旧 PDF。
- **双向定位**：支持文本、公式和项目内跨文件源码，结合编译器位置映射定位。
- **完整工作区**：文件树、项目搜索、默认展开的大纲、数学符号、问题面板、语法高亮与可调整分栏。
- **阅读与外观**：亮色／暗色主题、原色／夜间／护眼绿 PDF，以及简体中文和英文界面。
- **后台更新**：默认启动检查，后台下载和验签，不阻塞编辑；准备好后由用户选择安装重启，关闭前保护未保存内容。

## 安装

| 平台 | 系统要求 | 安装包 |
| --- | --- | --- |
| macOS | Apple Silicon，macOS 14 及以上 | [ARM64 DMG](https://github.com/liaoweiyang2017/typst-studio/releases/download/v0.2.2/Typst-Studio-0.2.2-macOS-arm64.dmg) |
| Windows ARM64 | Windows 10 build 19041 及以上，建议 Windows 11 | [ARM64 Setup](https://github.com/liaoweiyang2017/typst-studio/releases/download/v0.2.2/Typst-Studio-0.2.2-Windows-arm64-Setup.exe) |
| Windows x64 | Intel／AMD，Windows 10 build 19041 及以上，建议 Windows 11 | [x64 Setup](https://github.com/liaoweiyang2017/typst-studio/releases/download/v0.2.2/Typst-Studio-0.2.2-Windows-x64-Setup.exe) |

Mac 将应用拖入 Applications；Windows 运行安装器，可选择安装语言和是否创建桌面快捷方式。Windows 采用当前用户安装，附带 .NET／WinUI 运行时。

安装包包含 Typst 编译器，无需另装 Rust、Python 或编译环境。新引用的 `@preview` 包仍可能需要联网下载或已有缓存；文档要求的其他字体需自行安装。

当前 Mac 包为 ad-hoc 签名，尚未 Developer ID 公证；Windows 安装器尚未 Authenticode 签名。请从本仓库 Releases 下载。应用内更新另有开发者签名校验。

## 界面展示

### macOS

**全亮色界面 · 白色 PDF**

![macOS 亮色界面与白色 PDF](docs/screenshots/macos-editor-light.png)

**暗色界面 · 绿色护眼 PDF**

![macOS 暗色界面与绿色护眼 PDF](docs/screenshots/macos-editor-dark-green.png)

<details><summary>欢迎页与设置</summary>

![macOS 欢迎页](docs/screenshots/macos-welcome.png)
![macOS 设置](docs/screenshots/macos-settings.png)

</details>

### Windows

**全亮色界面 · 白色 PDF**

![Windows 亮色界面与白色 PDF](docs/screenshots/windows-editor-light.png)

**暗色界面 · 绿色护眼 PDF**

![Windows 暗色界面与绿色护眼 PDF](docs/screenshots/windows-editor-dark-green.png)

<details><summary>欢迎页与设置</summary>

![Windows 欢迎页](docs/screenshots/windows-welcome.png)
![Windows 设置](docs/screenshots/windows-settings.png)

</details>

截图来自原生应用运行的[示例工程](examples/showcase)，不包含私人文档。

## 示例

可从[基础排版](examples/hello.typ)、[数学公式](examples/math.typ)、[表格](examples/table.typ)或[多文件章节](examples/multi-file/main.typ)开始，均不依赖外部包。编译方式见[示例说明](examples/README.md)。

## 基本使用

1. 从欢迎页打开工程文件夹、`.typ` 文件或新建模板。
2. 选择入口文件（通常为 `main.typ`），点击“编译”。
3. 编辑入口或其包含的章节，自动编译更新当前入口；Windows 可在设置中关闭自动编译。
4. 使用双向定位检查 PDF 与源码；浏览其他文件不会把它自动编译为独立 PDF。
5. 如需单独编译另一个文件，打开它后再次点击“编译”。

PDF 与编译入口同目录、同名。PDF 使用本次编译的缓冲区内容，可能包含未保存编辑，但不会因此自动保存 `.typ` 源码。同名 PDF 被视为构建产物，会被更新覆盖；失败或取消不破坏上次成功输出。未命名文档需先保存源码，才能确定 PDF 输出路径。写入失败时保留预览并提示原因。

“导出 PDF”可选择其他保存位置。夜间和护眼绿只改变屏幕显示，不改变输出文件颜色。

### 常用快捷键

| 操作 | macOS | Windows |
| --- | --- | --- |
| 编译当前文件 | ⌘B | Ctrl+B |
| 保存源码 | ⌘S | Ctrl+S |
| 打开文件 | ⌘O | Ctrl+Shift+O |
| 打开文件夹 | ⇧⌘O | Ctrl+O |
| 查找 | ⌘F | Ctrl+F |
| 源码定位到 PDF | ⇧⌘J | Ctrl+Shift+J |
| PDF 选中文字定位源码 | ⌥⌘J | Ctrl+Alt+J |
| 点击 PDF 字形定位源码 | ⌘+单击 | Ctrl+单击 |
| 注释／取消注释 | — | Ctrl+/，可自定义 |
| 显示／隐藏侧栏 | 工具栏按钮 | Ctrl+Shift+B 或按钮 |

## 更新

默认每次启动检查一次，可在设置中关闭，也可手动检查。下载、校验在后台执行；完成后点击“安装并重启”，先处理未保存文件。取消保存、保存冲突或下载验证失败都不会强制退出编辑器。

Mac 沿用 Sparkle；Windows 使用 NetSparkle 核心与原生 WinUI 提示，通过 Inno 安装器更新。三个平台安装包与各自签名更新清单统一放在主仓库 Release。

**0.2.1 及更早版本用户需手动安装一次 0.2.2。** 旧发布仓库退役后，其 Mac 更新地址不再有效；旧 Windows 版本尚未内置更新器。0.2.2 起统一通过本主仓库获取后续更新。

构建、签名和发布均在本地完成，不使用 GitHub Actions。维护者流程见 [RELEASING.md](docs/RELEASING.md)。

## 源码与构建

- `macOS/`：Swift 包、Mac 原生界面、资源、测试和构建脚本。
- `Windows/`：WinUI 应用、C# 核心、测试、安装器与依赖说明。
- `examples/`：可公开分发的 Typst 示例。
- `docs/`：截图、版本说明和开发文档。
- `scripts/`：共享签名、验证和发布工具。
- `vendor/`：固定版本的 Tinymist 补丁及来源。

Mac 开发需 Apple Silicon、Xcode 命令行工具、Swift 和本地 Typst；首次构建 Tinymist 还需 Rust 1.92+。在仓库根目录运行：

```sh
bash macOS/scripts/test.sh
APP_DESTINATION="/tmp/Typst Studio.app" bash macOS/scripts/build-app.sh
APP_DESTINATION="/tmp/Typst Studio.app" bash macOS/scripts/package-dmg.sh
```

Windows 开发需 .NET 10、Visual Studio C++ 工具、Rust／LLVM 及 Inno Setup；固定依赖下载、校验和和打包步骤见 [Windows/README.md](Windows/README.md)。

```bat
cd Windows
tools\test.cmd
tools\build.cmd
```

可传 `arm64` 或 `x64` 限定目标。普通本地构建无需发布签名私钥。

## 当前边界

- 定位需要成功且最新的编译结果；未渲染语法、没有编译器 span 的图形无法定位，不猜测附近源码。
- 不自动打开项目外部包源码；同一宏位置可能对应多个输出位置。
- 平台功能存在差异：Mac 有原生拼写检查和独立 PDF 窗口；Windows 有行号开关和可配置注释快捷键，切换文件时会清除当前可见编辑器的撤销历史。
- Windows x64 在 ARM Windows 仿真环境验证，不代表所有实体 x64 设备已验证。具体证据见版本化 `specs/` 记录。

## 贡献与许可证

自有源码采用 [MIT](LICENSE) 许可证；第三方组件保留各自许可。参见 [Mac 声明](macOS/Resources/THIRD_PARTY_NOTICES.txt)、[Windows 声明](Windows/licenses/THIRD-PARTY-NOTICES.txt)、[Tinymist 补丁说明](vendor/README.md)。

参与贡献请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)，安全报告方式见 [SECURITY.md](SECURITY.md)。Typst Studio 是独立项目，并非 Typst、TeXstudio 或 Edist 的官方产品。
