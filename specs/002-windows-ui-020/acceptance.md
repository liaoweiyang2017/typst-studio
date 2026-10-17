# Windows 0.2.0 验收记录

2026-10-17。本轮保留 Swift Mac 与 Windows Core 编译、映射、保存实现；用户后续要求已纳入：格式图标完整显示，右下角显示 Typst Studio 0.2.0。

| 项目 | 状态 | 证据/限制 |
| --- | --- | --- |
| UI-01 设计令牌 | 通过 | App.xaml 合并 DesignTokens；动态UI也使用Ui资源适配；外观常量扫描与diff检查。 |
| UI-02 标题栏与Mica | 部分验证 | 实际Windows截图确认原生caption及暗色材质；RightInset与Passthrough使用系统API。真实鼠标拖动/菜单与跨显示器DPI仍需人工复核。 |
| UI-03 欢迎页尺寸 | 通过 | 原生smoke断言按钮320×40；960与1200 DIP窗口验证上下/左右布局并截图。 |
| UI-04 最近工程 | 通过，外部入口未实点 | ListView原生选择/hover，路径~和省略，完整路径tooltip；更多菜单仅移除记录或调用系统文件夹启动器。 |
| UI-05 三栏与隐藏侧栏 | 通过 | 原生smoke验证比例改变、默认均分、侧栏隐藏为0并恢复原宽度；分隔热区6/线1。 |
| UI-06 五面板与文件树 | 通过 | 原生回归验证跨文件搜索、展开大纲、符号过滤、错误及修复后清空；切换不改变缓冲区。 |
| UI-07 编辑器头与换行 | 通过 | 保留原生高亮/用户字号；首次布局迁移开启换行；行号/偏好保存回归。 |
| UI-08 查找与格式 | 通过 | 原生新增测试验证查找、替换全部的一次撤销、格式插入的一次撤销、浮层关闭、28 DIP按钮不被裁切。 |
| UI-09 PDF工具栏 | 通过，系统外部打开未实点 | 125%缩放及页码跳转断言，适宽、原生百分比输入截图；局部修正ComboBox编辑态padding。 |
| UI-10 纸张与映射 | 通过 | 24 DIP纸张间距/阴影截图；中文、公式、跨文件、旋转坐标测试；Core/导出字节处理未修改。 |
| UI-11 状态栏 | 通过 | 28 DIP、语义颜色、侧栏错误数；右下角按最新要求改软件名和版本。 |
| UI-12 主题/语言/键盘/DPI | 部分验证 | 原生Dark中文/Light英文、设置无溢出、截图及偏好持久化通过。VM实际200%缩放；100/125/150%和完整Tab遍历未实测，不标通过。 |
| REG-01 原有功能回归 | 通过 | ARM64/x64核心测试：原子保存、CRLF、外部冲突、实时编译、错误恢复、选择/渲染、重复公式/跨文件映射；原生smoke补充编辑撤销和五面板。x64在ARM Windows仿真运行。 |
| REL-01 构建与发布 | 通过 | 五阶段Debug x64与最终Release ARM64/x64成功，0警告/0错误。本地Inno安装包生成；隔离AppId测试安装后两架构原生smoke成功。私有源码已推送，公开windows-v0.2.0已发布，GitHub资源SHA256与本地完全一致。 |

## 测试证据

本地VM日志 C:\TypstStudio-build\stage1-020.log 至 stage5-020.log；核心 tests-020.log。原生回归输出 Windows/artifacts/qa020/native-smoke.json 和截图（生成产物忽略Git）。当前Debug x64报告success=true，version=0.2.0，rasterizationScale=2。

原生真实窗口截图用于确认Mica及系统caption。RenderTargetBitmap不包含系统材质/按钮，测试截图暂用系统实色背景覆盖后恢复，不能据此声称检验了Mica透明程度。

## 发现与修正

- Compact AppBarButton仍有系统模板尺寸，导致36 DIP格式栏裁切。改原生Subtle Button 28 DIP和16 DIP字形，不复制控件模板。
- 附件部分Segoe字形对应错误图标。按官方字形表修正；数学使用Cambria Math的根号/求和。
- ComboBox普通Padding不控制编辑态，改局部ComboBoxEditableTextPadding资源覆盖。
- Window内AdaptiveTrigger未在原生测试稳定切换；改Root.SizeChanged按实际DIP宽度切换，960/1200回归通过。
- 最近记录加载前清空并去重，防止同一进程重载设置出现重复；路径禁用换行。

未完成的人工交互及指定DPI验证仍保留为验收缺口，不以编译成功替代。

## 最终交付

- 实现提交：4470893（前四阶段分别85edc94、3f94c81、7d6b9f5、0b7af6b）。
- [公开 Windows 0.2.0 Release](https://github.com/liaoweiyang2017/typst-studio-releases/releases/tag/windows-v0.2.0)。未新增/运行GitHub Actions，Mac更新源保持原状。
- 本地安装包：Windows/artifacts/installers/Typst-Studio-0.2.0-Windows-{arm64,x64}-Setup.exe。
- 安装测试采用与发行版相同payload的独立QA AppId/安装目录，避免覆盖现有安装；测试后移除QA安装。安装后结果见evidence/installed-arm64.json及installed-x64.json。
- [校验和](evidence/SHA256SUMS-0.2.0.txt)。两种安装包都包含Tinymist/PDFium/运行时，无PDB。

## 修改文件范围

App.xaml与Styles/DesignTokens.xaml集中资源；MainWindow.Chrome/Welcome负责原生标题栏与欢迎页；MainWindow.Files/Sidebar及SidebarTabs负责文件与五面板；MainWindow.EditorTools/PreviewTools和SourceEditor/PdfViewer负责界面命令与布局；MainWindow.Settings、Ui、UiText负责设置与语言；MainWindow.Smoke及原有smoke入口补充回归；版本、installer、README、debug-x64工具与Spec文档同步更新。Windows Core、vendor和Mac源码没有改动。
