# 0.2.1 验收记录

实施授权：用户明确确认按钮编译当前文件并设为入口、编辑仍自动更新；普通切换和反向定位不编译。

## 复现与修复

原始Mac定向命令：swiftc使用SourceMappingTests同一真实模型/编译器依赖编译/tmp/TypstStudio-CompileTargetProbe.swift，再运行/tmp/typst-studio-compile-target-probe。输出：

```
PASS: exact identical-formula spans, math identifiers, cross-file mapping, Chinese scalar/UTF-16 positions and rotated frames
PASS: source revision changes cancel pending navigation
FAIL: inverse navigation scheduled a compile after opening the included source
```

原因：openFile无论来源均scheduleCompile，普通打开还清除原有pin。修复为浏览不修改入口、不增加内容revision；显式命令选取入口，自动编译捕获编辑快照并更新固定入口。切换前提交已有待处理编辑快照，避免保存后立即编辑另一文件丢失内存源更新。

Windows原来已固定工程入口，新增显式当前文件编译。Tinymist pinMain切换复用进程，关闭旧preview任务再创建新映射，串行gate保护任务和缓存。UI按revision/operation拒收过期结果，PDF解码完成后再次验证归属。

## 验收

| ID | 结果/证据 |
| --- | --- |
| CT01 | Mac DocumentSwitch通过：浏览不编译、不换入口/PDF；Windows native smoke加入同一断言。 |
| CT02 | Mac SourceMappingTests通过新增“不进入compiling”断言；中文、重复公式、旋转公式、跨文件定位保持通过。 |
| CT03 | Mac包含文件未保存修改更新完整入口PDF；新增保存后立即编辑子文件测试通过，不丢主文件修改。Windows核心未保存依赖测试通过。 |
| CT04 | Mac原生窗口实际点击验证：chapter浏览仍main PDF，点击Compile变chapter，再浏览main仍chapter PDF，点击恢复main。两端菜单/快捷键复用显式命令。 |
| CT05 | Windows native smoke加入连续编译不同入口断言；后台串行pinMain/preview切换，旧请求不能发布。最终Release两架构隔离安装与原生回归均通过，见evidence/installed-*.json。 |
| CT06 | Mac全量回归通过失败旧预览/缓存失效；Windows新增显式失败保留旧PDF且pdfRevision失效断言。错误通知晚到时问题列表先显示导出错误正文，不猜测行号。 |
| CT07 | Mac全量scripts/test.sh通过；原生Mac工具栏可访问且实际点击通过。Windows Debug x64与Release ARM64/x64通过；最终安装后原生回归均通过。 |
| CT08 | Mac arm64应用/DMG/签名已生成，Ed25519验证通过；Windows安装包已生成，两架构安装后回归通过，发布与远程校验待完成。 |

## 环境与限制

macOS Apple Silicon原生测试；Windows 11 ARM虚拟机，同时运行ARM64和x64仿真。保留0.2.0记录的Windows 100/125/150%DPI人工验收缺口，不声称本次已补齐。不改Mac/Windows的UI框架和编译器版本。

Mac暂存到Documents目录时出现Finder元数据导致codesign拒绝；改在/tmp构建签名，再生成仓库dist中的DMG。实际包签名与挂载检查通过后再发布。

## 最终本地验证

- Mac全量测试见evidence/mac-tests.log；EMDoc 182页真实工程回归见evidence/mac-emdoc.log。0.14ms为缓存PDF同步恢复时间，不是完整编译耗时。
- Windows核心测试见evidence/windows-core.log；安装后测试见evidence/installed-arm64.json和installed-x64.json。ARM首次启动没有产出报告，单独启动重跑后得到success=true；x64首轮安装后报告成功。
- Mac DMG只读挂载后codesign --verify --deep --strict通过，主程序arm64、内置Tinymist版本与既有0.15.10补丁一致，DMG的Sparkle Ed25519签名通过OpenSSL3公钥验证。
- 公开仓库已有自动发布清单工作流已按“不用GitHub Actions”要求设为disabled_manually。复用经过检查的清单脚本在本地执行；私有仓库没有工作流。
