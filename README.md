# Game Template

Unity 2022.3 3D URP game project template backed by the independently versioned Alloy Framework package.

## Setup

Clone this repository, then run `setup.bat` on Windows or `./setup.sh` on macOS/Linux. The setup script clones Alloy Framework into the ignored `AlloyFramework/` directory and applies repository-local Git settings.

Open `UnityProj` as the Unity project. Its package manifest references Alloy Framework through `file:../../AlloyFramework`, so framework edits are compiled immediately.

The game template and framework remain separate repositories. Commit game changes from the repository root and framework changes from `AlloyFramework/`.

## PulseVideoPlayer 首帧测试

当前 manifest 通过 `file:../../../PulseVideoPlayer/Packages/com.pulse.videoplayer` 引用播放器。
请将 `git@github.com:Jin8868/PulseVideoPlayer.git` 克隆到 game-template 的同级目录 `PulseVideoPlayer`。
现有 setup 脚本不会自动克隆 PulseVideoPlayer；播放器仓库包含 Windows x64 DLL。

在 Unity 中选择 `★AlloyFramework★/视频/创建或打开首帧测试场景`，然后按顶部 Play。
F5 是框架启动入口。独立测试场景在编辑器中跳过框架和业务启动，正常 Boot 启动保持原流程。
测试场景不在默认 Build Settings 中；按钮和资源采样只在编辑器或 Development Build 启用。

测试素材不提交：按播放器 Tests/README.md 准备后复制到 `UnityProj/Assets/StreamingAssets/PulseTests`。
读取说明保留在该目录 README.md 中。正式发布前移走本地测试素材，Git 忽略不影响 Unity 的资源收集。
当前阶段仅支持 Windows x64 / D3D11 首帧显示，尚未实现连续播放、音频和 Seek。
