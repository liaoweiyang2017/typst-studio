# Typst Studio Windows / WinUI 3 可行性分析

日期：2026-10-16。本轮只研究，没有创建 Windows 应用或修改参考仓库。

## 建议路线与结论

可以做成真正的 WinUI 3 应用。建议保留 macOS 的 SwiftUI/AppKit/PDFKit，另建 C#/.NET + WinUI 3 外壳，复用 Tinymist 编译、源码映射协议、测试文档和应用行为规范。不要为共享窗口代码把现有 Mac UI 一起重写成 Qt。

难度中高。窗口、设置、最近工程和三栏布局属于较低风险；专业编辑器、PDF 文本选择/连续滚动和两种 Windows 架构的稳定打包才是主要成本。参考 HarborFiles 后，单人全职做出可发布版本仍估计需要 6–10 人周，而不是几天换皮。该范围是工程估计，依赖编辑器与 PDF 后端验证结果。

## 三个仓库分别可借鉴什么

### HarborFiles：最接近目标技术栈

检查主分支 `c4621ebc11af843712532db03ca21978d1e6a82a`。`HarborFiles.App.csproj` 使用 `UseWinUI=true`、.NET Windows 目标、Windows App SDK 自包含部署，支持 ARM64/x64。App/Core/Windows 的分离方式适合借鉴，但 Typst Studio 不需要它的 Explorer 扩展、索引服务和文件管理器接管逻辑。[工程文件](https://github.com/liaoweiyang2017/HarborFiles/blob/c4621ebc11af843712532db03ca21978d1e6a82a/src/HarborFiles.App/HarborFiles.App.csproj)。

可参考其 SettingsView、UpdateCoordinator、PendingUpdateStore 和原生窗口布局，按本软件需要缩减。其 `PdfPreviewReader` 使用 Windows.Data.Pdf，按需渲染、限制并发并采用有限缓存。这适合文件快速预览，不能直接提供 TeXstudio 式的完整 PDF 阅读器。[PDF 预览代码](https://github.com/liaoweiyang2017/HarborFiles/blob/c4621ebc11af843712532db03ca21978d1e6a82a/src/HarborFiles.Windows/PdfPreviewReader.cs)。

### Modernterm：跨平台适配边界的参考

检查远端主分支 `09c52b99462ed4b4eebd96ca099001e5e37f3230`，版本 0.5.1；本机旧检出版本较早，评估以远端为准。最新 CMake 已区分 Windows 与 Apple 平台源文件，Windows 窗口材质通过 DwmSetWindowAttribute 设置。主体仍是 Qt Widgets/QFluentKit，不是真正 WinUI 3。[CMake](https://github.com/liaoweiyang2017/Modernterm/blob/09c52b99462ed4b4eebd96ca099001e5e37f3230/CMakeLists.txt)、[Windows 材质适配](https://github.com/liaoweiyang2017/Modernterm/blob/09c52b99462ed4b4eebd96ca099001e5e37f3230/src/native_material_win.cpp)。

可借鉴平台服务独立封装、睡眠/恢复、更新/凭据存储分离的做法；不能把 Qt Fluent 控件当作 WinUI 3 控件移植。终端的 PTY、SSH、SFTP 不属于本项目需求。

### TeXstudio：PDF 交互和渲染调度参考

参考 `PDFDocument.cpp`、`pdfrendermanager.cpp`、`qsynctex.cpp`。可借鉴可见页优先渲染、有限缓存、缩放后的渐进清晰显示、正反向定位、高亮和位置恢复。不要连同它的 Qt、Poppler、配置系统及 LaTeX 构建管理整体搬入。[PDF 代码目录](https://github.com/texstudio-org/texstudio/tree/master/src/pdfviewer)。

TeXstudio 的 SyncTeX 不能直接用于 Typst。Windows 应继续使用 Tinymist 的源码 span 与页面坐标，再转换成 PDF 视图坐标。Qt 或 WinUI 的选择本身不会提高编译速度，也不会生成源码映射。

## 推荐组件边界

| 层 | 建议 | 主要验证点 |
| --- | --- | --- |
| 窗口与设置 | WinUI 3 原生 NavigationView/TreeView、分组设置、系统标题栏、主题与 Mica | Windows 版本差异、键盘导航、缩放和高对比模式 |
| 编译与映射 | 内置 Windows Tinymist helper，经 stdio LSP 与本地映射连接通信 | ARM64/x64 构建、未保存文件、进程退出、超时、编译快照一致性 |
| 文本编辑器 | 先验证原生 Scintilla/WinUIEdit 路线；不要把普通 TextBox 当作成熟代码编辑器 | 中文 IME、撤销、长文、行号、Typst 高亮、选区与 UTF-16 转换 |
| PDF | 优先验证 PDFium + 原生页面视图/选择覆盖层；或先用 Windows.Data.Pdf 做无选择的验证版 | 选中文字、字形坐标、连续滚动、页面虚拟化、DPI、夜间配色 |
| 更新发布 | 参考 HarborFiles 的后台下载/待安装状态，Windows 独立安装器 | 文件占用、回滚、重启、签名、ARM64/x64 资产匹配 |

WinUIEdit 的上游 README 明确表示仍处于开发阶段、尚非生产就绪，因此应作为验证候选，不能直接承诺它能满足全部编辑需求。[WinUIEdit](https://github.com/BreeceW/WinUIEdit)。如果不得不用 Monaco/WebView2，可以保持外层 WinUI 3，但编辑区域就不再是纯原生控件，应提前明确这个取舍。

Windows.Data.Pdf 官方 API 提供页面渲染，现有 HarborFiles 也主要输出页面位图。要做文本选择、查找高亮及精确点击，需要额外的文本几何能力；PDFium 的文本 API 提供字符框和位置命中接口，可作为后端候选。[微软页面渲染 API](https://learn.microsoft.com/en-us/uwp/api/windows.data.pdf.pdfpage.rendertostreamasync)、[PDFium 文本接口](https://pdfium.googlesource.com/pdfium/+/refs/heads/main/public/fpdf_text.h)。

## 工期估计

假设一名熟悉 C#/WinUI 3 与原生互操作的开发者全职，复用现有需求与测试用例，先支持 Windows 11，并能使用真实 Windows ARM64 和 x64 环境验收。

| 阶段 | 人日估计 |
| --- | ---: |
| 编辑器/PDF 原型验证，确认架构与许可证 | 3–5 |
| 工程、文件树、设置、语言、快捷键、窗口布局 | 5–8 |
| 编辑器与 Tinymist 编译、诊断、双向映射接入 | 7–11 |
| PDF 连续预览、选择、高亮、缓存与配色 | 7–12 |
| 安装更新、签名、ARM64/x64 回归与真实文档测试 | 6–10 |
| 合计 | 28–46 人日，约 6–10 人周 |

2–3 周可以争取做出打开工程、编辑、编译与基本预览的演示版，但不能视为完整稳定版。若编辑器候选验证失败、需要自研选择层或支持复杂 PDF 旋转/混合页面，再增加 2–4 周缓冲。费用应按人日乘团队成本计算，本报告没有假设人工报价或商业许可证价格。

## 许可与维护风险

TeXstudio 渲染源码带有 GPL 条款；直接复制实现后公开分发，需要履行相应许可证义务，私人源码仓库不能代替向接收者提供所需源码。应优先参考行为和调度方案，独立实现。Qt、PDFium、Scintilla/WinUIEdit 也应按实际采用版本核验许可证与第三方清单。[TeXstudio 许可证](https://github.com/texstudio-org/texstudio/blob/master/COPYING)。

最有价值的跨平台共享成果是编译/映射协议、错误与缓存规则、测试文档和版本清单，而不是强行共享 Swift/WinUI 视图代码。macOS 与 Windows 保留各自原生界面，可以减少平台体验上的妥协。
