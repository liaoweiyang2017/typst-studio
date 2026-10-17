# 技术方案与文件清单

依附件阶段清单和用户三项确认执行。阶段门禁：每阶段 Debug x64 成功后再提交并进入下一阶段。

## 已定位的现有文件

- Windows/src/TypstStudio.App/MainWindow.xaml：欢迎页、菜单、三栏、编辑头、PDF工具栏、状态栏。
- MainWindow.xaml.cs：标题栏初始化、欢迎页/最近工程、菜单命令、查找、状态更新和原生测试入口。
- MainWindow.Sidebar.cs：五个面板与下拉选择；只换选择器/外观，保留后台搜索和导航接口。
- MainWindow.Settings.cs：设置视图、主题、分栏、偏好和快捷键。
- App.xaml：合并资源；App.xaml.cs：启动与诊断。
- SourceEditor.xaml / SourceEditor.cs：Scintilla宿主、样式、换行和选择/编辑适配。
- PdfViewer.cs：纸张容器、页间距/阴影、UI缩放和导航状态；不改底层渲染及映射逻辑。
- PaneDivider.cs：分隔线与热区；UiText.cs：本地化。
- Windows/tools/build.cmd、installer/TypstStudio.iss、Directory.Build.props：构建、版本与打包。

## 计划新增

Styles/DesignTokens.xaml（令牌和基础样式）、UI资源读取适配、标题栏/欢迎页/编辑交互相关 partial 文件；仅按实际职责拆分，不引入MVVM框架或新编辑器。

## 保持不变的核心

Windows/src/TypstStudio.Core 的 TinymistSession、LspConnection、PdfDocument、SourceCoordinates 和现有文件保存规则；vendor、native编译器、Mac Sources 不因UI重构变化。若需要新增文件创建辅助，放独立最小UI命令，不改原有保存冲突语义。

## 实现路径

1. 资源字典合并并统一C#动态UI样式读取，处理主题变化；图标和布局尺寸都使用资源。资源合规扫描检查XAML与动态UI，不用简单字符串搜索误报ThemeResource表达式。
2. 扩展标题栏，菜单Passthrough区域与空白拖动区域分别设置，跟踪SizeChanged/DPI/RightInset。保留原生caption按钮，不手绘红绿灯。
3. 欢迎页使用受约束Grid和ListView，右列360；VisualStateManager在1040DIP切换布局。去掉双重菜单和额外ScrollViewer。
4. SelectorBar优先，必要时仅因已证实API/布局限制使用原生ToggleButton分段替代并记录证据。面板本体持续保留。
5. 查找浮层使用现有编辑器选区/替换适配，格式命令使用原生undo分组；新建目录/文件通过用户确认的位置和防覆盖检查。
6. PDF新增只读展示属性/交互适配，复用Zoom/FitWidth/FitPage/GoTo/NavigateSelection；纸张边距和命中测试共享同一布局参数，不改坐标映射算法。外部打开不修改源PDF。
7. 保留原文快捷键，增加不冲突的命令；Tooltip/AutomationProperties使用同一命令描述。

## 研究依据

- https://github.com/microsoft/WinUI-Gallery/blob/main/WinUIGallery/Samples/SelectorBar/SelectorBarPage.xaml
- https://github.com/microsoft/WinUI-Gallery/blob/main/WinUIGallery/MainWindow.xaml.cs
- https://learn.microsoft.com/en-us/windows/apps/develop/title-bar
- https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/title-bar
- https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/selector-bar

现有包为Microsoft.WindowsAppSDK.WinUI 2.3.6，不能把它误读为旧WinUI 2；先在固定依赖上编译验证SelectorBar/标题栏API，不为外观盲目升级SDK。

## 风险和验证

标题栏交互区域、Scintilla主题色、PDF纸张边距对应的点击坐标、最小尺寸下工具栏溢出是重点。每阶段记录Debug x64命令与结果；最后跑实际编译器/PDF/源码映射和原生窗口回归。100/125/150%视觉结果逐项记录，未实测的比例不能标为通过。x64在ARM虚拟机运行需明确限制。
