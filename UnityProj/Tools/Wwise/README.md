# Wwise SoundBank

项目使用 Wwise **2025.1.11.9262**，Unity Integration **2025.1.11.4331**。

首次克隆项目、清理生成目录或修改音频后，在 UnityProj 目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Wwise\GenerateSoundBanks.ps1
```

其他机器安装目录不同，可使用 `-WwiseInstallationPath 'D:\Audiokinetic\Wwise_2025.1.11.9262'`，
或设置 `WWISE_INSTALLATION_PATH` 环境变量。正式构建前，还需在 Unity 的 Wwise Integration 设置中
设置本机 Wwise Installation Path；构建前已启用生成和复制 SoundBank。

脚本读取 `Assets/WwiseSettings.xml` 的工程和输出路径，生成 Windows SoundBank，并复制到
`Assets/StreamingAssets/Audio/GeneratedSoundBanks/Windows`。
Unity 编辑器优先从 `../WwiseProj/WwiseAudio/GeneratedSoundBanks/Windows` 读取音频；
构建时由 Wwise 插件复制到 StreamingAssets。Wwise 工程的 Root Output Path 保持 `GeneratedSoundBanks/`，
Windows 输出保持 `GeneratedSoundBanks/Windows/`。

提交 Wwise 源工程（`.wproj`、`.wwu`、Originals 原始音频等）、Unity 集成脚本、原生插件、
配置与对应 `.meta`。不提交 SoundBank 生成目录、转换缓存、个人设置、安装压缩包、日志、
离线帮助文档与调试符号。不要忽略整个 `Assets/Wwise` 或所有 `.dll`，否则其他机器无法编译和运行。
