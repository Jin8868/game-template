# Wwise SoundBank

项目使用 Wwise **2025.1.11.9262**，Unity Integration **2025.1.11.4331**。

Windows 平台已配置 Wwise 的 Post-Generation Step。重新加载 Wwise 工程后，
点击 `Generate Checked` 或 `Generate All`，生成结束会自动调用项目 `ExportAudioContent.ps1`，
导出框架清单与原始文件收集源，不需要另开 PowerShell。
在生成日志中确认“框架音频清单已导出”和“RawFile 收集目录”；出现导出错误时先修复错误。
EditorSimulate 退出再进入运行即可；Offline/Host 仍需通过 Unity 菜单重新构建 AudioPackage。

首次克隆项目、清理生成目录或修改音频后，打开 Wwise 工程执行生成，生成后步骤会自动导出。
项目已删除手动生成脚本入口，保留 `ExportAudioContent.ps1` 与 `ResolveFrameworkTools.ps1`。
已有生成结果时，也可在 UnityProj 目录单独重新导出：

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Wwise\ExportAudioContent.ps1 -Platform Windows
```

导出工具读取 `Assets/WwiseSettings.xml` 中的输出路径，
导出依赖清单与 `Assets/Res/WwiseAudio/Windows` 原始文件收集源。
框架接管后不再由此脚本复制到官方 StreamingAssets 目录。
Wwise 工程的 Root Output Path 保持 `GeneratedSoundBanks/`。
运行前的场景、官方自动初始化和资源收集配置见 [框架音频说明](FrameworkAudio.md)。

提交 Wwise 源工程（`.wproj`、`.wwu`、Originals 原始音频等）、Unity 集成脚本、原生插件、
配置与对应 `.meta`。不提交 SoundBank 生成目录、转换缓存、个人设置、安装压缩包、日志、
离线帮助文档与调试符号。不要忽略整个 `Assets/Wwise` 或所有 `.dll`，否则其他机器无法编译和运行。
