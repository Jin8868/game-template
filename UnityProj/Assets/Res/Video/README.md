# Pulse 视频资源

此目录由 VideoPackage 的 RawFile 采集器收集，只收集 `.pulse`。
不要把视频打进普通 AssetBundle，不要把 Wwise 音轨或 FFmpeg 放到此目录。

- `Synthetic/all_in_one.pulse` 的资源地址为 `Video/Synthetic/all_in_one`。
- `Sintel/Sintel.2010.1080p.pulse` 的资源地址为 `Video/Sintel/Sintel.2010.1080p`。
- 视频本体及其文件 `.meta` 忽略 Git，克隆仓库后自行准备素材。
- 本次已从宿主 StreamingAssets/PulseTests 复制 Synthetic，用于本机测试。

准备素材后使用项目既有的一键主包或补丁构建入口。
构建流程将 VideoPackage 和对应版本加入发布清单，并使用 RawFileBuildPipeline。
CDN 部署时发布完整发布目录，包含 VideoPackage 的清单和构建文件，资源地址不是 CDN 文件 URL。
Host 模式使用当前发布清单中的 VideoPackage 版本，由 YooAssets 完成下载和缓存。
直接提供 HTTP/HTTPS `.pulse` URL 时，宿主下载器先下载到独占临时文件，关闭后删除。
当前不支持边下载边播放。

加载 UI 直接设置 Image.fillAmount 和 Text 百分比，不旋转图像。
整体准备进度分为视频资源 0%～60%、音频预载完成 80%、首帧就绪 95%、播放准备完成 100%。
直接 URL 的实际下载进度映射到前 60%；当前 YooAssets 原始文件接口不提供连续进度，按阶段完成更新。
本地准备较快时允许百分比跳变，完成后隐藏加载提示，不使用计时模拟进度。

## 播放入口

工程使用 Game.Video.GameVideoPlayer 组件，内部创建 VideoPlaybackCoordinator 并注入同物体的 PulseVideoPlayer。
VideoPreviewUIController 只获取 GameVideoPlayer，业务无需创建协调器或调用 SetPlaybackDriver。
提交版本使用 Inspector 配置，不包含本地联调的硬编码视频地址、音频事件和诊断日志。
现有 VideoPreviewUI 预制体已添加封装组件；其他预制体在原播放器物体上添加 GameVideoPlayer 即可。
底层 PulseVideoPlayer 保留原有画面、加载提示、按钮和进度条引用，GameVideoPlayer 配置视频资源包名称。
封装组件禁用时关闭音画并禁用底层交互，重新启用后可再次 Play；销毁时释放协调器。
组件本身不引用 Alloy、YooAssets 或 Wwise；其他项目可实现 IPulsePlaybackDriver 替换宿主逻辑。
Game.Runtime 必须引用 PulseVideoPlayer 和 PulseVideoPlayer.UI；完整类型名不能替代 asmdef 引用。

```csharp
// player 是预制体上的 Game.Video.GameVideoPlayer。
player.Play("Video/Synthetic/all_in_one");

await player.PlayAsync(
    videoLocation: "Video/Synthetic/all_in_one",
    audioEventKey: null,
    loopMode: PulseVideoPlayer.EPulseLoopMode.Count,
    playCount: 3,
    cancellationToken: cancellationToken);
```

Once 播放一次，Infinite 播放到主动停止，Count 的次数包含第一次播放。
PlayAsync 等待资源就绪及开始播放，不等待视频结束。
Completed 在最后一轮结束时触发，循环期间复用视频文件、纹理和材质。
无参 Play 和 Replay 优先使用最近请求；首次使用 Inspector 中的 Source Path、Audio Event Key、Loop Mode、Play Count。
资源地址应关闭 Relative To Streaming Assets；相对 StreamingAssets 的本地路径则开启该项。
打开预览界面的动画结束后自动调用 Play，Open On Enable 保持关闭，避免在打开动画之前提前播放。

## Wwise 视频音轨

1. 将与视频时间轴一致的独立音轨导入 Wwise，按平台转换并生成 Bank/媒体。
   现有 `.audio.m4a` 不是本接口的播放参数，可先离线转换为 WAV 再用于 Wwise 制作。
2. 建立专用的单一音轨事件，例如 `Play_Video_Sintel`；不要加入随机容器、额外延迟、循环或变速。
3. 使用工程既有的 Wwise 导出流程更新 Assets/Res/WwiseAudio 的内容和依赖清单，再构建 AudioPackage。
4. 将事件 Key 填到组件的 Audio Event Key，或作为 PlayAsync 的 audioEventKey 参数。

本地联调可使用 Video/Synthetic/all_in_one 和 Play_Video_AllInOne，模式为 Once。
测试入口与 Wwise 测试工程改动不提交；提交版本保留原预制体的本地视频配置，音频事件为空。
接入音轨前需在实际 Wwise 工程制作专用事件并生成 SoundBank；尚未制作或导出时播放会报错。
只测试无音轨视频时，Play 的 audioEventKey 参数传 null。
不能仅将任意 m4a/wem 路径填进事件 Key，也不能将音轨改名为 wem 代替 Wwise 转码。
框架新增可选 IAudioTimelineBackend，Wwise 实现按 Playing ID 定位和读取位置，不影响同名事件的其他实例。
暂停同时冻结音画；Seek 先等待视频目标帧，再定位音轨并恢复原播放状态。
视频作为主时钟，播放期间每 0.5 秒查询音轨位置；偏差超过 150ms 时请求校正。
这是按视频帧和 Wwise 命令调度的同步，不承诺音频采样级同步；必须使用目标平台及实际媒体验收。
Wwise 主动结束较短音轨后允许视频继续静音，下一轮或后续 Seek 可重新提交该音轨。

关闭时立即请求停止声音，音频模块继续持有原生播放所需依赖到结束通知。
视频文件租约则等准备操作退出、原生解码与 GPU 退役完成后再释放。
取消 PlayAsync 的令牌会终止本次播放生命周期；取消当前 Seek 会结束当前会话，更新的 Seek 可以替代旧等待。

## 手动验收

本次未执行 Unity 编译、原生构建或运行测试；继续使用 ABI 3 DLL，无需因托管适配重建 DLL。
本次增加框架 AOT API，发布时先完整出包建立新基线，不向旧 AOT 主包直接发布新增 API 的补丁。

1. 编辑器模拟模式从首页打开预览，确认使用 VideoPackage 的 Synthetic 资源且关闭无错误。
2. 测试 Count=3 和 Infinite，确认循环不重复下载，Completed 只在最后一轮触发。
3. 使用真实 CDN URL，观察真实下载百分比；下载中关闭再打开，旧请求不能覆盖新界面。
4. 制作音轨事件后验证播放、暂停、恢复、前后 Seek、循环和关闭；不要用短按钮音效代替完整音轨验收。
5. 在 Host 模式使用新发布清单验证 VideoPackage 的下载及 YooAssets 缓存命中。
6. 定位或下载时关闭预览；关闭完成后独占的 AlloyRawFiles 快照和 PulseDownloads 目录应释放。

## Synthetic 音轨导入与预览验收

1. 将 Tests/Generated/Synthetic/all_in_one.audio.m4a 转为 PCM WAV，保留完整时间轴，不剪掉开头静音。
2. 打开 game-template/WwiseProj/WwiseAudio/WwiseAudio.wproj，在 Containers 中导入为 Sound SFX。
3. 为该声音创建仅含一个 Play 动作的事件 Play_Video_AllInOne，关闭声音循环、延迟和随机化。
   首轮使用 PCM 转换和非流式播放减少变量；后续再验证压缩格式与流式媒体定位。
4. 生成 Windows SoundBank。工程使用自动 Event Bank，生成后步骤自动导出到
   Assets/Res/WwiseAudio/Windows，并更新 AlloyAudioManifest.json。
5. 确认清单包含 Play_Video_AllInOne，退出运行模式后重新从框架启动场景运行。
   EditorSimulate 可直接读取导出内容；Offline/Host 必须重新构建并交付 AudioPackage。
6. 从首页进入视频预览，Console 查看 [PulsePreview] 请求、首帧和完成日志，实际听音核对音画。
   暂停时声音停止推进；恢复连续播放；前后拖动进度条后音轨应定位到同一时刻。
7. 播放中关闭界面应停止声音；再次打开从头播放，无叠音。
   测试循环时将 Controller 的调用改为 Count、playCount 为 3，确认播放三轮后仅一次最终完成日志。

首帧就绪不等于音频已经启动；没有异常也不能代替听音和拖动后的同步验收。
