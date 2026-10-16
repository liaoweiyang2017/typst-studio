# Typst Studio 的 Qt 迁移成本评估

评估日期：2026-10-16。范围为现有原生 macOS 编辑器及 Windows 版本的可能性。本轮只研究，不实现 Qt 分支。

## 建议

如果近期仍然只发布 macOS，继续 SwiftUI + AppKit + PDFKit 的投入更小。Qt 的主要价值是共享 Windows/macOS 的 UI 和文档管理逻辑，不会自动让 Typst 编译更快，也不会自动得到精确的源码映射。

如果明确要同时维护 Windows 和 macOS，建议用 Qt Widgets 做技术验证，再决定迁移。先验证三件事：大型中文文档的输入和撤销、180 页以上 PDF 的滚动与缓存、Typst 编译器的增量编译与定位。不要先复制 TeXstudio 的整个界面。

## 仓库调研结果

本次检查 TeXstudio 主分支提交 `ac3dc9b75e01a0796a904667aa163b3e696acc1f`，Tinymist 主分支提交 `45b50f0d928e825248fa1c2fcbcd68549e957b5d`。

TeXstudio 的 PDF 预览不是单个可直接替换 PDFKit 的独立控件。`PDFDocument.cpp` 负责交互与文档显示，`pdfrendermanager.cpp` 管理缓存和渲染任务，`pdfrenderengine.cpp` 执行后台渲染，`qsynctex.cpp` 负责 SyncTeX 映射。它们还依赖 TeXstudio 自身的配置管理、工具函数和其他界面组件。[PDFDocument](https://github.com/texstudio-org/texstudio/blob/ac3dc9b75e01a0796a904667aa163b3e696acc1f/src/pdfviewer/PDFDocument.cpp)、[PDFRenderManager](https://github.com/texstudio-org/texstudio/blob/ac3dc9b75e01a0796a904667aa163b3e696acc1f/src/pdfviewer/pdfrendermanager.cpp)。

值得参考的是它的工作方式：后台渲染队列、优先处理可见页面、缓存已有图像并随后细化、限制缓存占用、针对高倍率局部渲染。现有应用使用 PDFKit，部分渲染调度由系统完成；迁移后需要自己承担更多缓存、线程和页面更新工作。不能仅根据使用 Qt 就推断滚动速度一定更快。[渲染管理实现](https://github.com/texstudio-org/texstudio/blob/ac3dc9b75e01a0796a904667aa163b3e696acc1f/src/pdfviewer/pdfrendermanager.cpp)。

TeXstudio 使用 SyncTeX 的源文件、行号和 PDF 页面坐标映射；Typst 0.15.1 CLI 的 `compile --help` 没有对应输出选项。因此拿过来它的跳转按钮或解析器，也不会产生 Typst 的精确定位数据。Tinymist 的预览与编辑器集成值得进一步验证，但它不是 PDFKit/Qt PDF 的直接替代组件。[SyncTeX 包装代码](https://github.com/texstudio-org/texstudio/blob/ac3dc9b75e01a0796a904667aa163b3e696acc1f/src/pdfviewer/qsynctex.cpp)、[Tinymist 编辑器集成](https://github.com/Myriad-Dreamin/tinymist/blob/45b50f0d928e825248fa1c2fcbcd68549e957b5d/editors/vscode/README.md)。

## 迁移边界

| 模块 | 可以保留的内容 | 需要重新实现的内容 |
| --- | --- | --- |
| Typst 编译 | Typst 二进制、文档样例、watch 进程策略、行为测试用例 | Swift Process、异步调度、监视和错误处理改为 QProcess 等 Qt 接口 |
| 文本编辑 | 语言规则、配色与交互设计 | NSTextView 的输入法、撤销、选区、补全、行号、查找全部重新接入 |
| PDF | 阅读位置模型、配色偏好、缓存原则 | PDFKit 替换为 Poppler 或 Qt PDF；选中文字、坐标、高亮、渲染队列重新验证 |
| 工程和设置 | 文件发现规则、设置标识、中英文词条 | QFileSystemModel/QSettings、最近项目、侧栏与设置窗口 |
| 更新和发布 | 公开发布仓库、版本策略 | macOS 可继续桥接 Sparkle；Windows 需要独立安装更新与签名流程 |
| 外观 | 已选图标、三栏布局 | 原生控件样式、菜单、快捷键、高 DPI、深色模式的平台差异 |

目前 Swift 源码约两千行，项目尚小，但系统框架承担了很多没有显式出现在行数里的功能。不能用逐行翻译的方式估算成本。

## 工作量估计

以下是工程估计，假设一名熟悉 C++/Qt、macOS 和 Windows 发布的开发者全职工作，并能及时获得实际文档测试反馈。不是已经测得的开发时长。

| 阶段 | 估计人日 | 验收内容 |
| --- | ---: | --- |
| 技术验证与选型 | 3–5 | 编辑器输入法、PDF 连续预览、大文档性能、依赖与许可方案 |
| 工程、编辑器及编译链 | 10–15 | 打开项目、撤销、保存、错误恢复、watch 编译、跨文件工作 |
| PDF 阅读与双向定位交互 | 8–12 | 缓存、缩放、搜索、高亮、颜色、位置恢复；此处仍为文本匹配 |
| 设置、语言及平台交互 | 4–7 | 中英文、设置迁移、系统菜单、文件对话框、窗口行为 |
| 双平台打包、更新与回归 | 8–12 | arm64 Mac、Windows 安装包、更新失败恢复、真实文档压力测试 |
| 合计 | 33–51 | 约 7–11 人周；实际排期预留到 8–13 周 |

如果还要求公式、自动生成内容、重复文本都能精确定位，需要另外评估编译器源码映射或 Tinymist 适配；建议额外预留 2–4 周验证预算，验证后再承诺实现排期。若只是做可运行演示，可压缩到 2–3 周，但不能视作可替代当前版本的稳定产品。

费用可按“实际人日 × 团队日成本”计算。这里没有假设当地人工报价、商业 Qt 授权价格或代码签名账号费用。

## 开源许可与复用选择

TeXstudio 上述渲染文件标注 GPL-2.0-or-later。直接移植这些实现并分发衍生应用，会涉及 GPL 的源码提供等要求；仅把应用源码留在私人仓库不能代替向接收者履行要求。可以参考功能和结构，独立实现，不复制代码。Qt 以及 PDF 后端的许可证还需要按最终版本和模块单独核验，不能把“Qt 支持 LGPL”视作所有模块及后端均相同。[TeXstudio COPYING](https://github.com/texstudio-org/texstudio/blob/ac3dc9b75e01a0796a904667aa163b3e696acc1f/COPYING)、[Qt 官方许可说明](https://doc.qt.io/qt-6/licensing.html)。

结论：当前以 macOS 为主时建议继续原生实现。如果 Windows 已进入正式产品计划，再投入 3–5 人日完成上述验证，然后根据结果选择 Qt PDF、Poppler 或其他后端，重新收敛整体工期。
