# 技术方案

状态：已确认，按用户最终选择实施。

1. 先复现并锁定Mac反向定位及后续普通打开/编辑触发的预览变化；Windows补同一契约测试。现有Mac SourceMappingTests已覆盖页面保留但仍等待重编译完成，需增强为不触发导航编译。
2. 在现有StudioModel会话/Windows _entry与TinymistSession上明确编译目标的所有权。复用pinMain与现有包含文件缓冲更新；导航只更新编辑状态，不借此重置目标或重启服务。
3. 将自动更新固定预览与显式编译当前文件分开。按钮/快捷键复用显式命令；状态/提示显示入口，失败保留预览且禁止过期映射。
4. 原生工具栏添加编译按钮；编译进度只影响编译控件，避免锁整个窗口。UI尽量使用两端已有资源和本地化。
5. 先定向真实Tinymist/原生模型测试，再完整相关回归；本地构建和隔离安装验证，然后发布。

## GitHub研究依据

- TeXstudio FAQ明确区分编辑文件和root文件，编辑子文件时编译root：https://github.com/texstudio-org/texstudio/wiki/Frequently-Asked-Questions
- TeXstudio源码文档区分编译与PDF逆向搜索：https://github.com/texstudio-org/texstudio/blob/master/utilities/manual/source/viewing.md
- Tinymist语言服务器/预览架构：https://github.com/Myriad-Dreamin/tinymist/blob/main/docs/dev-guide.md
- 项目当前已使用tinymist.pinMain及Tinymist源码定位接口，不需要引入另一套预览后端。

## 文件范围

Mac StudioModel.swift、App.swift、StudioLanguage.swift与必要的TinymistCompiler会话适配；Windows MainWindow编译/导航命令、EditorTools/XAML/UI文字与必要的TinymistSession入口适配；Tests/SourceMappingTests、DocumentSwitch、Windows native smoke。版本及本地打包脚本按现有发布流程更新。

## 性能验证

同一固定入口切换编辑文件不重启Tinymist，不额外编译；同一测试工程记录首编译和热编译数据。功能回归优先以请求/入口/页面状态断言，不将机器速度差异当作功能失败。
