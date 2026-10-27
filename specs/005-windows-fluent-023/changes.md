# Windows 0.2.3 修改文件与需求对照

日期：2026-10-27。macOS 源码、版本与二进制保持 0.2.2。

| 文件 | 对应要求 | 改动 |
| --- | --- | --- |
| Windows/src/TypstStudio.App/Assets/FileIcons/folder.svg | 一.1 | 黄色收起文件夹 |
| Windows/src/TypstStudio.App/Assets/FileIcons/folder-open.svg | 一.1 | 黄色展开文件夹 |
| Windows/src/TypstStudio.App/Assets/FileIcons/typ.svg | 一.1 | 蓝底白色 t 文档 |
| Windows/src/TypstStudio.App/Assets/FileIcons/pdf.svg | 一.1 | 红色 PDF 文档 |
| Windows/src/TypstStudio.App/Assets/FileIcons/json.svg | 一.1 | 橙色花括号文档 |
| Windows/src/TypstStudio.App/Assets/FileIcons/markdown.svg | 一.1 | 灰蓝色 M↓ 文档 |
| Windows/src/TypstStudio.App/Assets/FileIcons/image.svg | 一.1 | 绿色图片 |
| Windows/src/TypstStudio.App/Assets/FileIcons/terminal.svg | 一.1 | 深灰色终端脚本 |
| Windows/src/TypstStudio.App/Assets/FileIcons/document.svg | 一.1 | 默认灰色文档 |
| Windows/src/TypstStudio.App/FileIcons.cs | 一.2 | 单一扩展名字典，缓存 SvgImageSource；未知类型直接使用默认图标 |
| Windows/src/TypstStudio.App/MainWindow.Files.cs | 一.2、5 | 图标与入口字重属性，展开/收起图标通知，入口变更刷新 |
| Windows/src/TypstStudio.App/MainWindow.xaml | 一.3–4、三.1、四.2、4 | SVG 模板、正文样式、原生选中态、强调编译按钮、圆角表面与字数状态栏 |
| Windows/src/TypstStudio.App/SidebarTabs.cs | 二 | 复用 SelectorBar，统一 Fluent FontIcon；InfoBadge 锚定问题项 |
| Windows/src/TypstStudio.App/MainWindow.Sidebar.cs | 二 | 数学符号入口采用 Fluent 计算器图标 |
| Windows/src/TypstStudio.App/MainWindow.EditorTools.cs | 三.4、四.4、后续图标调整 | 统一 AppBarButton，七个 16 DIP 矢量格式图标，按内容变化更新字数 |
| Windows/src/TypstStudio.App/Styles/DesignTokens.xaml | 一.3、三.2、4、四.2 | 32 DIP 树行、96 DIP 缩放框、统一格式按钮样式及布局尺寸 |
| Windows/src/TypstStudio.App/SourceEditor.cs | 四.3、定位提示补充 | 按字体和 DPI 计算约 1.5 倍原生行距；源码目标行三秒提示 |
| Windows/src/TypstStudio.App/SourceEditor.xaml | 四.3、定位提示补充 | 次要行号颜色、主题色定位边框与半透明背景 |
| Windows/src/TypstStudio.App/PdfViewer.cs | 三.3、四.5、定位提示补充 | 预览背景采用主题表面；保留白纸/护眼设置和阴影；放大定位框并三秒清除；移除固定蓝色 |
| Windows/src/TypstStudio.App/Ui.cs | 四.5 | 根据控件当前主题解析 ThemeResource，修复应用内切换主题后的原生编辑器画刷 |
| Windows/src/TypstStudio.App/MainWindow.xaml.cs | 一.5、定位提示补充、验证 | 编译入口变更触发字重更新；反向定位触发源码提示；扩展原生验收 |
| Windows/src/TypstStudio.App/MainWindow.Smoke.cs | 验收 | SVG 解码、文件夹状态、字重、徽标、主题、行距、字数和格式命令回归；更新真实截图 |
| Windows/src/TypstStudio.App/TypstStudio.App.csproj | 一.1、打包 | SVG 资源复制到发布目录 |
| Windows/src/TypstStudio.App/MainWindow.Settings.cs | 版本 | 关于页显示 Windows 0.2.3 |
| Windows/Directory.Build.props | 版本 | Windows 包版本 0.2.3 |
| Windows/src/TypstStudio.App/app.manifest | 版本 | Windows manifest 版本 0.2.3 |
| Windows/installer/TypstStudio.iss | 发布 | Windows 安装器与文件名 0.2.3 |
| scripts/publish-windows-release.sh | 发布 | 独立 Windows 发布，保留原有 Mac 安装包、feed 和清单 |
| README.md、README_CN.md、Windows/README.md | 文档 | 分平台版本、下载地址、界面特性与截图 |
| docs/Release-0.2.3.md、docs/RELEASING.md | 发布 | 英文说明与 Windows 独立发版流程 |
| docs/screenshots/windows-editor-light.png、windows-editor-dark-green.png、windows-welcome.png、windows-settings.png | 展示 | 实际 Windows 0.2.3 截图 |
| specs/005-windows-fluent-023/* | 规范与验收 | 需求、方案、任务、结果、文件清单和原生报告 |

## 取舍

- 复用已存在的 Mica、ExtendsContentIntoTitleBar、SelectorBar、InfoBadge 和阅读模式；没有新增服务、配置项、特性开关或资源回退链。
- 文件图标为项目自绘的 16×16 SVG 路径，不依赖 SVG 字体。格式图标使用统一的主题色 PathIcon，避免不同字体造成尺寸、字重与基线混杂。
- 符号库入口使用 Segoe Fluent Icons 的计算器图形；实际数学符号插入功能保留。
- 护眼绿默认关闭适用于新配置；尊重用户已经保存的阅读颜色选择。
- 字数按连续非汉字字母/数字段以及单个汉字统计，包含当前编辑源码；移动光标不会重新扫描内容。
- 无实体 x64 机器验证；x64 在 Windows ARM 虚拟机的仿真环境运行。所有编译/运行/调试在 Parallels 内完成，未在 Mac 安装 .NET。
