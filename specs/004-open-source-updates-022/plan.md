# 0.2.2 技术方案

状态：已确认。

1. 在两端“接受最新成功PDF”的位置接入小型同目录输出组件。重用原始PDF字节，后台写临时文件并替换，按入口和版本串行/合并；写入结果独立于排版成功状态。覆盖不可写、只读、锁定、链接、失败及快速连续编译。
2. Mac复用AutomaticUpdater/SPUUserDriver，仅补齐启动/状态/发布一致性与回归，不另写更新器。Windows锁定NetSparkle稳定core包，先用最小WinUI可运行验证确认API/线程/安装参数，再接一个原生状态服务与设置/InfoBar。依赖API若与研究源码不符，先记录并修正方案，不堆兼容层。
3. Windows使用独立签名的ARM64/x64 appcast；固定公钥，Strict验签。采用本地签名工具与离线密钥保管，绝不把私钥放仓库、命令输出或Release。Inno更新模式只在成功后重启一次，不强制杀进程。
4. 先核心逻辑测试再隔离安装更新测试，验证脏文档取消、网络失败和包验证。主仓库同一v0.2.2 Release承载全部平台；latest按各平台/架构的独立feed文件选择包，下载URL和架构验证仍严格。
5. 整理公开README（主README英文、README_CN中文）、版本说明、贡献与许可；使用examples/showcase等演示文档生成真实截图。清理陈旧“仅Mac/Windows尚未实现/依赖Actions”等说明。
6. 公开前检查当前树和全部历史。初步扫描24个提交、388个blob：未发现常见私钥头、GitHub令牌、AWS access key或敏感密钥文件名。历史中有开发规范、测试记录和本地路径，用户选择是否连同历史公开。
7. 本地构建与校验后上传0.2.2产物；更新feed及签名，验证远端内容，再完成开源可访问性检查。无需Actions。按用户追加指令，主仓库发布验证完成后删除旧发布仓库与旧Release。

## 主要改动范围

Mac StudioModel/PDF输出小组件、AutomaticUpdater/Settings、Info.plist；Windows MainWindow编译与退出接入、更新状态服务、设置/状态UI、核心输出/更新测试、csproj依赖与Inno更新模式；scripts签名/发布；README、许可证、截图、示例与文档。

## 复用依据

详见research-updates.md，使用官方NetSparkle/Sparkle源码与Inno/微软文档。保留现有框架，不增加自研下载验签/安装进程框架。
