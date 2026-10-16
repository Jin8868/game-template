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
提交版本使用 Inspector 配置，不包含本地联调的硬编码视频地址、音频事件和测试打印。
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
6. 定位或下载时关闭预览；关闭完成后归还原文件租约并释放 PulseDownloads 临时目录，不删除源文件。

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

## 外置 SRT 字幕

PulseVideoPlayer 组件新增“字幕 / Subtitle Text”引用，拖入普通 Unity UI Text，不使用 TMP。
Text 所在物体及父物体保持激活，宽度按实际视频区域的 80%～90% 设置，放在底部控制栏上方。
组件设置自动换行、垂直溢出和纯文本显示，字号、字体、对齐方式和排版宽度由预制体配置。
建议关闭 Best Fit；中文字体需包含相应字形。无字幕、关闭、失败或最后一轮结束后清空文字。

```csharp
// 无字幕入口保持兼容。
player.Play(videoLocation, audioEventKey);

// 带字幕入口；路径按视频、音轨事件、字幕排列。
player.Play(
    "Video/Synthetic/Sintel.2010.1080p",
    "Play_Sintel",
    "Video/Synthetic/Sintel.2010.1080p.en",
    PulseVideoPlayer.EPulseLoopMode.Once,
    1);

// 也有 PlayAsync；开关只影响显示，不重新加载字幕。
player.SetSubtitlesEnabled(false);
player.SetSubtitlesEnabled(true);
```

字幕和视频可放在同一目录，例如 Sintel.pulse、Sintel.en.srt、Sintel.zh-CN.srt。
字幕导入器将 UTF-8 .srt 转为 TextAsset，DefaultPackage 的 videoSubtitles 组只采集 .srt；
VideoPackage 仍只收集 .pulse。Host/Offline 部署时需要交付更新后的 DefaultPackage。
原始字幕读取还支持绝对本地路径、file URI 和 HTTP/HTTPS URL；资源地址不加 Assets/Res 前缀。

本地联调可使用 Sintel 英文字幕，示例文件位于
Assets/Res/Video/Synthetic/Sintel.2010.1080p.en.srt，来自 Generated/Sintel 的 subtitle1.srt。
该测试文件及 meta 忽略 Git。第一句在 1:47.250 开始，可拖到 1:48 验证，之前不显示字幕是正常的。

SRT 一次解析并保留条目、时间边界和当前显示文字，TextAsset 句柄与文件句柄在读取后释放。
使用视频时钟查询，不另启计时器；二分查找时间区间，跨边界时才更新文字，支持暂停、Seek、循环、
重播、多行、乱序和重叠条目。播放新视频时旧字幕自动清空，无字幕入口不会沿用上次字幕。
SRT 样式标签转换为纯文本，不实现 ASS 动效、逐字高亮或最多两行分页，长文本按 Text 宽度自动换行。
播放器包仅依赖可选 IPulseSubtitlePlaybackDriver，YooAssets 加载仍在工程侧实现，不修改原生 DLL。

手动验证：拖入 Text 后打开预览，在 1:48 附近暂停、前后拖动并恢复，观察字幕是否与对白一致；
开关字幕、关闭重开以及从带字幕切换无字幕请求，确认没有残留。未执行编译或运行测试。

带字幕的 Play / PlayAsync 使用三个字符串参数的视频、音轨、字幕顺序；无音轨时显式传 null，避免和原有无字幕重载混淆。

## 播放流程日志

编辑器和 Development Build 仅输出 [Pulse][操作编号] 的关键流程开始、完成、取消及失败。
播放、定位、暂停、恢复、循环和关闭保留结果；原有异常堆栈及 Wwise 初始化错误继续输出。
不再输出逐步骤计时、文件吞吐、校验细节、内存采样或音量探测日志。

已有物理文件时，资源层直接借用文件，保持 YooAssets 句柄与读取租约，不建立完整副本。
视频等待原生与 GPU 退役后归还租约；音频等待 Wwise 卸载后归还。
Wwise 对原名文件直接登记读取目录，改名缓存只建立文件名硬链接，不复制数据。
硬链接受同卷、文件系统及权限限制，失败明确报错，不静默回退完整复制。
跨卷、只读且改名的部署需要调整缓存位置或接入 Wwise 自定义 I/O。
同名文件仍在使用时禁止切换来源或摘要；切换已登记的读取目录需先关闭音频会话。

AudioSettings.VerifyContentHashes 默认 false：资源包文件跳过音频层额外 SHA256，
依赖当前 YooAssets 文件系统校验设置；编辑器模拟文件不具备下载校验保证。
普通本地文件及无物理路径的落地文件仍校验。严格比对导出清单时可启用该开关。
启用后按 256 KiB 分块只读校验，Windows 优先使用系统 CNG，其他平台使用默认实现。
APK 内等无物理路径来源仍需落地，CDN 视频仍需下载。

框架 ClearUnusedCacheAsync 等待同包直读租约归还后清理；清理期间阻止新直读进入。
音频闲置缓存及 Init 组可能持续持有租约，清理 AudioPackage 前需卸载缓存并关闭音频会话。
等待可取消；直接调用 YooAssets 清理、销毁资源包或手动删除文件不受该协调保护。
改动均在 C#，无需重编 Pulse 播放器 DLL。未执行编译或运行测试。

## 业务播放接口

业务使用 GameVideoPlayer，视频、音轨、字幕的参数顺序保持不变。原有 Play 重载继续从零开始。
首次起点使用新重载；不必在 Play 后立刻 Seek。无字幕可省略字幕参数，无音轨显式传 null。

```csharp
player.Play(videoLocation, audioEventKey, subtitleLocation, startTimeSeconds: 4.0);
player.Play(videoLocation, audioEventKey, startTimeSeconds: 4.0);
player.Play(videoLocation, null, startTimeSeconds: 4.0);

await player.PlayAsync(videoLocation, audioEventKey, subtitleLocation,
    startTimeSeconds: 4.0, cancellationToken: cancellationToken);
```

起点要求非负有限秒数，超过时长时定位末帧。先等待目标帧完成再绑定画面和发出 Ready，
Wwise 在一次主线程后端提交中执行 PostEvent 和首次 Seek，之后启动视频时钟。
这不是音频采样级或跨设备时钟同步保证；Wwise 的定位限制和流式缓冲仍然适用。
Ready 表示目标画面就绪，Started 表示视频开始推进；PlayAsync 结束表示已启动，不等待视频播完。
首次从指定位置开始，后续循环从零开始；Replay 和 ReplayAsync 也从零重播，并重置循环计数。

| 接口 | 行为 |
| --- | --- |
| Play / PlayAsync | 无字幕、有字幕及指定起点重载；Once、Infinite、Count 循环策略 |
| Pause / Resume / TogglePause | 暂停、恢复及在两者间切换 |
| Replay / ReplayAsync | 使用最近一次地址、音轨、字幕和循环配置，从零重播 |
| Seek / SeekAsync | 按秒定位，保留跳转前的播放或暂停状态 |
| SeekNormalized / SeekNormalizedAsync | 按零至一的进度定位，超出范围会钳制 |
| Close / CloseAsync | 立即请求关闭，或等待原生退役与文件租约归还 |
| SetSubtitlesEnabled | 显示或隐藏字幕，不重新加载文件 |

状态查询包括 State、IsLoading、LoadingProgress、IsBusy、IsPlaying、IsPaused、IsSeeking、
CanSeek、CanPause、CanResume、PositionSeconds、DurationSeconds、NormalizedProgress、LastError。
保留毫秒单位的 GetPlayTimeMs，PositionSeconds 使用秒，NormalizedProgress 使用零至一。
GameVideoPlayer 转发 Ready、Started、Paused、Resumed、Stopped、Completed、Failed 和 LoadingProgressChanged。
Stopped 仅表示画面解绑并请求关闭；必须 await CloseAsync 才能确认资源退役完成。
普通 Seek 在准备期间仍会拒绝执行；首次定位应传起点参数，或等待 PlayAsync 后再 SeekAsync。
没有增加与 Replay 重复的 Restart，也没有在业务组件中另加与 Close 重复的 Stop。

Pulse 组件通过可选 IPulsePlaybackControlDriver 获得起点和可等待重播能力，原有驱动接口不变。
AudioPlayOptions.StartTimeMs 默认零；非零要求后端实现 IAudioTimelineStartBackend，当前 Wwise 已实现。
其他音频后端不支持时明确报错，不会先从零发声再延后纠正。

手动验证：从 4 秒启动，检查首个可见画面、音轨与字幕；播放中暂停、恢复、百分比跳转；
调用 ReplayAsync 检查回到零秒；Count 模式检查后续循环从零及最终 Completed；
在准备或起点定位期间关闭界面，确认旧请求不会补播。未执行编译或运行测试。
