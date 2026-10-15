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
播放器已加入 Windows x64 / D3D11 连续播放、暂停/恢复、Seek 与独立 UI 组件，并包含 ABI 3 DLL。首页按钮可打开 VideoPreviewUI，关闭流程等待视频资源退役。预制体默认使用 StreamingAssets/PulseTests/Sintel/Sintel.2010.1080p.pulse，需自行准备忽略 Git 的测试素材。音频计划通过 Wwise 适配，YooAssets 资源加载、带 URL 的播放接口和循环控制留待下一阶段。基础连续播放已验证，新增 Seek/UI 流程仍需用户运行验收；详见 PulseVideoPlayer/Native/Windows/README.md。

视频预览现使用 GameVideoPlayer，内部通过 VideoPlaybackCoordinator 注入资源与 Wwise 适配。VideoPackage 为原始视频包，示例地址为 Video/Synthetic/all_in_one；视频文件仍忽略 Git。Assets/Res/Audio 为空占位，已删除并移除旧采集规则，Wwise 内容保留在 Assets/Res/WwiseAudio。详细配置与音轨事件制作见 UnityProj/Assets/Res/Video/README.md。本阶段改动需要用户编译和运行验收。
