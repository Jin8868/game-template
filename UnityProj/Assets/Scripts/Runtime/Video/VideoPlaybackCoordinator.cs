using System;
using System.Threading;
using AlloyFramework.Audio;
using Cysharp.Threading.Tasks;
using PulseVideoPlayer;
using UnityEngine;

namespace Game.Video
{
    /// <summary>宿主的视频资源、Wwise 音轨、循环与生命周期协调器。</summary>
    public sealed class VideoPlaybackCoordinator : IPulsePlaybackDriver, IPulseSubtitlePlaybackDriver,
        IPulsePlaybackControlDriver, IDisposable
    {
        private readonly PulseVideoPlayer.UI.PulseVideoPlayer m_player; // 负责显示与原生视频控制的组件。
        private readonly string m_packageName; // YooAssets 视频原始文件包。
        private Request m_request; // 当前请求，迟到的异步结果必须核对此引用。
        private UniTask m_retirement = UniTask.CompletedTask; // 当前关闭链，可供多个等待者共享。
        private int m_generation; // 取消仍在等待上次关闭的新请求。
        private bool m_isDisposed; // 销毁后不再接受新的播放。
        private bool m_applicationPaused; // 应用在后台时禁止启动或自动恢复音画。
        private string m_lastLocation; // 最近一次业务视频地址，不保存临时下载路径。
        private string m_lastAudioKey; // 最近一次音轨事件。
        private string m_lastSubtitleLocation; // 最近一次外置字幕地址，无字幕请求会清空。
        private EPulseLoopMode m_lastLoopMode; // 最近一次循环策略。
        private int m_lastPlayCount = 1; // 最近一次指定播放次数。

        /// <summary>为组件创建宿主适配器，不初始化音频引擎。</summary>
        /// <param name="player">预制体上已有的播放器组件。</param>
        /// <param name="packageName">YooAssets 原始视频资源包名称。</param>
        /// <exception cref="ArgumentNullException">播放器为空。</exception>
        /// <exception cref="ArgumentException">资源包名称为空。</exception>
        public VideoPlaybackCoordinator(PulseVideoPlayer.UI.PulseVideoPlayer player,
            string packageName = "VideoPackage")
        {
            m_player = player != null ? player : throw new ArgumentNullException(nameof(player));
            if (string.IsNullOrWhiteSpace(packageName)) throw new ArgumentException("视频包名称不能为空。", nameof(packageName));
            m_packageName = packageName;
        }

        /// <summary>是否正在准备、跳转或重播。</summary>
        public bool IsBusy => m_request?.Busy ?? false;

        /// <summary>加载视频与 Wwise 依赖并开始播放，空音频事件表示静音视频。</summary>
        /// <param name="videoLocation">YooAssets 地址、本地路径或 HTTP/HTTPS URL。</param>
        /// <param name="audioEventKey">Wwise 事件 Key，不是 m4a 或 wem 文件路径。</param>
        /// <param name="loopMode">循环模式。</param>
        /// <param name="playCount">Count 模式下的总播放次数。</param>
        /// <param name="cancellationToken">本次播放生命周期的取消令牌。</param>
        /// <returns>音画已经启动的任务，不等待所有轮次播放结束。</returns>
        /// <exception cref="ArgumentException">视频地址为空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">循环策略或次数无效。</exception>
        /// <exception cref="ObjectDisposedException">协调器已销毁。</exception>
        /// <exception cref="InvalidOperationException">音频未就绪或不支持时间轴控制。</exception>
        public UniTask PlayAsync(string videoLocation, string audioEventKey, EPulseLoopMode loopMode,
            int playCount, CancellationToken cancellationToken)
        {
            return PlayCoreAsync(videoLocation, null, audioEventKey, loopMode, playCount, cancellationToken);
        }

        /// <summary>准备视频、外置 SRT 和可选 Wwise 音轨，并开始播放。</summary>
        /// <param name="videoLocation">YooAssets 视频地址、本地文件或 CDN URL。</param>
        /// <param name="audioEventKey">Wwise 音轨事件名，空值表示无音轨。</param>
        /// <param name="subtitleLocation">DefaultPackage 字幕地址、本地 SRT 文件或 CDN URL。</param>
        /// <param name="loopMode">循环模式。</param>
        /// <param name="playCount">指定总次数，包含第一次播放。</param>
        /// <param name="cancellationToken">本次播放生命周期令牌。</param>
        /// <returns>音画已启动的任务，不等待播放结束。</returns>
        /// <exception cref="ArgumentException">视频或字幕地址为空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">循环模式或次数无效。</exception>
        /// <exception cref="FormatException">字幕格式无效。</exception>
        /// <exception cref="ObjectDisposedException">协调器已经销毁。</exception>
        public UniTask PlayAsync(string videoLocation,
            string audioEventKey, string subtitleLocation,
            EPulseLoopMode loopMode, int playCount, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(subtitleLocation))
                throw new ArgumentException("字幕地址不能为空。", nameof(subtitleLocation));
            return PlayCoreAsync(
                videoLocation, subtitleLocation, audioEventKey, loopMode, playCount, cancellationToken);
        }

        /// <summary>先定位目标画面，再从同一时间启动音轨，字幕时间轴随视频时钟更新。</summary>
        /// <param name="videoLocation">视频资源地址。</param>
        /// <param name="audioEventKey">可选音轨事件。</param>
        /// <param name="subtitleLocation">可选字幕地址。</param>
        /// <param name="startTimeSeconds">首次起点秒数，后续循环从零开始。</param>
        /// <param name="loopMode">循环策略。</param>
        /// <param name="playCount">指定总次数。</param>
        /// <param name="cancellationToken">播放生命周期令牌。</param>
        /// <returns>目标画面和音轨已准备并启动的任务。</returns>
        /// <exception cref="ArgumentOutOfRangeException">起点或循环设置无效。</exception>
        /// <exception cref="ArgumentException">视频地址为空。</exception>
        /// <exception cref="ObjectDisposedException">协调器已经销毁。</exception>
        public UniTask PlayAsync(string videoLocation, string audioEventKey, string subtitleLocation,
            double startTimeSeconds, EPulseLoopMode loopMode, int playCount, CancellationToken cancellationToken)
        {
            return PlayCoreAsync(videoLocation, subtitleLocation, audioEventKey,
                loopMode, playCount, cancellationToken, startTimeSeconds);
        }

        private UniTask PlayCoreAsync(string videoLocation, string subtitleLocation, string audioEventKey,
            EPulseLoopMode loopMode, int playCount, CancellationToken cancellationToken)
        {
            return PlayCoreAsync(videoLocation, subtitleLocation, audioEventKey,
                loopMode, playCount, cancellationToken, 0);
        }

        private async UniTask PlayCoreAsync(string videoLocation, string subtitleLocation, string audioEventKey,
            EPulseLoopMode loopMode, int playCount, CancellationToken cancellationToken, double startTimeSeconds)
        {
            var trace = new VideoPlaybackTrace("播放准备");
            Request request = null;
            try
            {
                if (m_isDisposed) throw new ObjectDisposedException(nameof(VideoPlaybackCoordinator));
                if (string.IsNullOrWhiteSpace(videoLocation)) throw new ArgumentException("视频地址不能为空。");
                if (double.IsNaN(startTimeSeconds) || double.IsInfinity(startTimeSeconds) || startTimeSeconds < 0)
                    throw new ArgumentOutOfRangeException(nameof(startTimeSeconds));
                if (loopMode != EPulseLoopMode.Once && loopMode != EPulseLoopMode.Infinite &&
                    loopMode != EPulseLoopMode.Count)
                    throw new ArgumentOutOfRangeException(nameof(loopMode));
                if (loopMode == EPulseLoopMode.Count && playCount < 1)
                    throw new ArgumentOutOfRangeException(nameof(playCount));
                cancellationToken.ThrowIfCancellationRequested();

                // 旧会话完全退役后才加载新资源，计时包含其异步清理等待。
                UniTask retirement = CloseAsync();
                int generation = ++m_generation;
                await retirement;
                cancellationToken.ThrowIfCancellationRequested();
                if (generation != m_generation || m_isDisposed || m_player == null || !m_player.isActiveAndEnabled)
                    throw new OperationCanceledException(cancellationToken);

                request = new Request(this, cancellationToken, audioEventKey, loopMode,
                    loopMode == EPulseLoopMode.Once ? 1 : playCount);
                m_request = request;
                m_lastLocation = videoLocation;
                m_lastAudioKey = audioEventKey;
                m_lastSubtitleLocation = subtitleLocation;
                m_lastLoopMode = loopMode;
                m_lastPlayCount = playCount;
                m_player.ShowPreparing();

                request.Video = await VideoResourceLoader.LoadAsync(
                    videoLocation, m_packageName, request, request.Token);
                EnsureCurrent(request);
                // 下载占总准备进度的前 60%，其余阶段完成后更新，不使用计时模拟进度。
                m_player.SetLoadingProgress(0.6f);
                if (!string.IsNullOrWhiteSpace(subtitleLocation))
                {
                    request.Subtitles = await VideoSubtitleLoader.LoadAsync(subtitleLocation, request.Token);
                    EnsureCurrent(request);
                }
                if (!string.IsNullOrWhiteSpace(audioEventKey))
                {
                    if (!AudioManager.Instance.SupportsTimeline)
                        throw new InvalidOperationException("视频音轨需要已初始化且支持 Seek/位置查询的音频后端。");
                    request.AudioScope = AudioManager.Instance.CreateScope("VideoPlayback");
                    await AudioManager.Instance.PreloadAudioAsync(audioEventKey, request.AudioScope, request.Token);
                    EnsureCurrent(request);
                }
                m_player.SetLoadingProgress(0.8f);

                await m_player.PrepareVideoAsync(request.Video.FilePath, startTimeSeconds, request.Token);
                EnsureCurrent(request);
                // 准备首帧会解除旧会话，字幕应在该阶段完成后再绑定。
                m_player.SetSubtitleTrack(request.Subtitles);
                m_player.SetLoadingProgress(0.95f);
                if (request.AudioScope != null) ToMilliseconds(m_player.DurationSeconds);
                await WaitForForegroundAsync(request);
                await StartAudioAsync(request);
                EnsureCurrent(request);
                await WaitForForegroundAsync(request);
                m_player.SetLoadingProgress(1);
                EnsureCurrent(request);
                request.Busy = false;
                request.NeedsForegroundSync = false;
                request.ResumeOnForeground = false;
                SetAudioPaused(request, false);
                m_player.StartPreparedVideo();
                EnsureCurrent(request);
                trace.Complete();
            }
            catch (Exception exception)
            {
                trace.Fail(exception);
                if (request != null && ReferenceEquals(m_request, request))
                {
                    CloseAsync().Forget(LogFailure);
                    if (!(exception is OperationCanceledException)) m_player.ReportPlaybackFailure(exception);
                }
                throw;
            }
            finally { request?.Prepared.TrySetResult(); }
        }

        /// <summary>暂停当前音画。</summary>
        /// <exception cref="InvalidOperationException">当前正在协调或后端拒绝暂停。</exception>
        public void Pause()
        {
            Request request = RequireActive();
            if (request.Busy) throw new InvalidOperationException("请等待音画准备或跳转完成。");
            if (m_player.State != EPulsePlayerState.Playing) return;
            var trace = new VideoPlaybackTrace("暂停");
            try
            {
                SetAudioPaused(request, true);
                m_player.PauseVideo();
                trace.Complete();
            }
            catch (Exception exception) { trace.Fail(exception); throw; }
        }

        /// <summary>从当前暂停位置恢复音画。</summary>
        /// <exception cref="InvalidOperationException">当前正在协调或后端拒绝恢复。</exception>
        public void Resume()
        {
            Request request = RequireActive();
            if (m_applicationPaused || request.NeedsForegroundSync || request.Recovering)
            {
                // 回前台的目标画面提交前，不能提前解除音轨暂停。
                request.ResumeOnForeground = true;
                return;
            }
            if (request.Busy) throw new InvalidOperationException("请等待音画准备或跳转完成。");
            if (m_player.State != EPulsePlayerState.Paused) return;
            var trace = new VideoPlaybackTrace("恢复");
            try
            {
                SetAudioPaused(request, false);
                m_player.ResumeVideo();
                trace.Complete();
            }
            catch (Exception exception) { trace.Fail(exception); throw; }
        }

        /// <summary>切后台时冻结音画，回前台后由 Tick 调度目标画面与音轨的重新同步。</summary>
        /// <param name="paused">应用是否已经进入后台。</param>
        public void SetApplicationPaused(bool paused)
        {
            if (m_applicationPaused == paused || m_isDisposed) return;
            m_applicationPaused = paused;
            Request request = m_request;
            if (!paused || request == null) return;

            try
            {
                // 仅记录原本的播放意图；手动暂停的视频回来后仍保持暂停。
                bool wasPlaying = m_player.State == EPulsePlayerState.Playing;
                request.ResumeOnForeground |= wasPlaying || (request.Seeking && request.ResumeAfterSeek);
                request.NeedsForegroundSync |= wasPlaying || request.Seeking ||
                    m_player.State == EPulsePlayerState.Paused;
                request.ResumeAfterSeek = false;
                if (wasPlaying) m_player.PauseVideo();
                SetAudioPaused(request, true);
                Debug.Log($"[VideoLifecycle] 已切后台，位置={m_player.PositionSeconds:F3}s，" +
                    $"回前台自动播放={request.ResumeOnForeground}。");
            }
            catch (Exception exception) { Fail(exception); }
        }

        /// <summary>重新提交最近的业务地址和循环配置，首次播放使用组件 Inspector 配置。</summary>
        public void Replay()
        {
            ReplayAsync(default).Forget(LogFailure);
        }

        /// <summary>使用最近的地址、音轨、字幕及循环配置从零重播，重置已播放轮次。</summary>
        /// <param name="cancellationToken">新请求的播放生命周期令牌。</param>
        /// <returns>新一轮音画已经启动的任务。</returns>
        /// <exception cref="ObjectDisposedException">协调器已经销毁。</exception>
        public UniTask ReplayAsync(CancellationToken cancellationToken)
        {
            string location = m_lastLocation ?? m_player.ConfiguredVideoLocation;
            string audioKey = m_lastLocation == null ? m_player.AudioEventKey : m_lastAudioKey;
            EPulseLoopMode mode = m_lastLocation == null ? m_player.LoopMode : m_lastLoopMode;
            int count = m_lastLocation == null ? m_player.PlayCount : m_lastPlayCount;
            return PlayCoreAsync(location, m_lastSubtitleLocation, audioKey, mode, count, cancellationToken);
        }

        /// <summary>先冻结音画，定位视频，再将音轨定位到同一时刻并恢复原来的播放状态。</summary>
        /// <param name="timeSeconds">目标秒数，会钳制到视频范围。</param>
        /// <param name="cancellationToken">取消本次等待，播放生命周期由请求本身持有。</param>
        /// <returns>音画定位请求完成的任务。</returns>
        /// <exception cref="ArgumentOutOfRangeException">时间不是有限数值。</exception>
        /// <exception cref="InvalidOperationException">没有活跃请求或音轨定位失败。</exception>
        public async UniTask SeekAsync(double timeSeconds, CancellationToken cancellationToken)
        {
            if (double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds))
                throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            Request request = RequireActive();
            if (request.Busy && !request.Seeking) throw new InvalidOperationException("视频仍在准备或重播。");
            bool resume = request.Seeking ? request.ResumeAfterSeek : m_player.State == EPulsePlayerState.Playing;
            int serial = ++request.SeekGeneration;
            request.ResumeAfterSeek = resume;
            request.Seeking = true;
            request.Busy = true;
            var trace = new VideoPlaybackTrace("跳转");
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(request.Token, cancellationToken))
            {
                try
                {
                    if (m_player.State == EPulsePlayerState.Playing) m_player.PauseVideo();
                    SetAudioPaused(request, true);
                    double target = Math.Max(0, Math.Min(timeSeconds, m_player.DurationSeconds));
                    await m_player.SeekVideoAsync(target, linked.Token);
                    EnsureCurrent(request);
                    if (serial != request.SeekGeneration) throw new OperationCanceledException(linked.Token);
                    if (request.AudioScope != null)
                    {
                        if (request.PlayID == 0 || request.AudioEnded)
                        {
                            await StartAudioAsync(request);
                        }
                        EnsureCurrent(request);
                        // 只有新启动的音轨才需要再次暂停，旧音轨已在定位前暂停。
                        SetAudioPaused(request, true);
                        if (!AudioManager.Instance.SeekAudio(request.PlayID, ToMilliseconds(target)))
                            throw new InvalidOperationException("Wwise 未接受音轨定位，请检查事件是否为可定位的单一音轨。");
                        if (request.ResumeAfterSeek && !m_applicationPaused && !request.NeedsForegroundSync)
                            SetAudioPaused(request, false);
                    }
                    request.Busy = false;
                    request.Seeking = false;
                    if (request.ResumeAfterSeek && !m_applicationPaused && !request.NeedsForegroundSync)
                        m_player.ResumeVideo();
                    trace.Complete();
                }
                catch (Exception exception)
                {
                    trace.Fail(exception);
                    // 被更新的 Seek 替代时保留新请求；其他错误停止整个会话，避免只播其中一路。
                    if (ReferenceEquals(m_request, request) && serial == request.SeekGeneration)
                    {
                        if (exception is OperationCanceledException) CloseAsync().Forget(LogFailure);
                        else Fail(exception);
                    }
                    throw;
                }
                finally
                {
                    if (serial == request.SeekGeneration)
                    {
                        request.Busy = false;
                        request.Seeking = false;
                    }
                }
            }
        }

        /// <summary>停止音轨和视频，等待准备收尾及原生文件读取退出后释放资源。</summary>
        /// <returns>可重复等待的清理任务。</returns>
        public UniTask CloseAsync()
        {
            m_generation++;
            Request request = m_request;
            m_request = null;
            if (request == null) return m_retirement;

            // 完成源支持多个挂起的等待者；Preserve 仅缓存结果，不能并发等待同一个状态机。
            var completion = new UniTaskCompletionSource();
            m_retirement = completion.Task;
            request.CloseTrace = new VideoPlaybackTrace("关闭");
            try
            {
                request.Cancellation.Cancel();
                if (request.PlayID != 0) AudioManager.Instance.StopAudio(request.PlayID);
                request.AudioScope?.Dispose();
            }
            catch (Exception exception)
            {
                request.CloseTrace.Fail(exception);
                throw;
            }
            finally
            {
                // 立即捕获旧播放器的关闭任务，且只由退役流程消费一次。
                UniTask videoClosed = m_player != null ? m_player.CloseVideoAsync() : UniTask.CompletedTask;
                RetireAsync(request, videoClosed, completion).Forget(LogFailure);
            }
            return m_retirement;
        }

        /// <summary>检查取消和音频错误，并以视频时钟纠正持续的音轨偏差。</summary>
        public void Tick()
        {
            Request request = m_request;
            if (request == null) return;
            if (request.Token.IsCancellationRequested || m_player.State == EPulsePlayerState.Faulted)
            {
                CloseAsync().Forget(LogFailure);
                return;
            }
            if (m_applicationPaused || request.Busy || request.Recovering) return;
            if (request.NeedsForegroundSync)
            {
                RecoverForegroundAsync(request).Forget(LogFailure);
                return;
            }
            if (request.PlayID == 0 || request.AudioEnded) return;
            try
            {
                AudioManager audio = AudioManager.Instance;
                if (!audio.IsReady) throw new InvalidOperationException("视频播放期间音频后端已关闭。");
                if (audio.TryGetPlayback(request.PlayID, out AudioPlaybackInfo info))
                {
                    if (info.State == EAudioPlaybackState.Failed)
                        throw new InvalidOperationException(info.Result.Message);
                    if (info.State == EAudioPlaybackState.Ended) { request.AudioEnded = true; return; }
                }
                if (m_player.State != EPulsePlayerState.Playing ||
                    Time.realtimeSinceStartupAsDouble < request.NextSyncTime) return;
                request.NextSyncTime = Time.realtimeSinceStartupAsDouble + 0.5;
                if (audio.TryGetAudioPosition(request.PlayID, out int position) &&
                    Math.Abs(position - m_player.GetPlayTimeMs()) > 150 &&
                    !audio.SeekAudio(request.PlayID, ToMilliseconds(m_player.GetPlayTimeMs() / 1000)))
                    throw new InvalidOperationException("音轨偏差校正失败。");
            }
            catch (Exception exception) { Fail(exception); }
        }

        /// <summary>处理单轮结束，自动循环时抑制最终完成通知。</summary>
        /// <returns>所有指定轮次结束时为 true。</returns>
        public bool OnVideoCompleted()
        {
            Request request = m_request;
            if (request == null) return true;
            request.CompletedCount++;
            if (request.PlayID != 0) AudioManager.Instance.StopAudio(request.PlayID);
            request.PlayID = 0;
            request.AudioEnded = true;
            if (request.LoopMode != EPulseLoopMode.Infinite && request.CompletedCount >= request.PlayCount) return true;
            request.Busy = true;
            RestartLoopAsync(request).Forget(LogFailure);
            return false;
        }

        /// <summary>请求最终清理，后续不再允许播放；资源清理由帧循环继续完成。</summary>
        public void Dispose()
        {
            if (m_isDisposed) return;
            m_isDisposed = true;
            CloseAsync().Forget(LogFailure);
        }

        private async UniTask RestartLoopAsync(Request request)
        {
            var trace = new VideoPlaybackTrace("循环重播");
            try
            {
                await m_player.SeekVideoAsync(0, request.Token);
                EnsureCurrent(request);
                await WaitForForegroundAsync(request);
                await StartAudioAsync(request);
                EnsureCurrent(request);
                await WaitForForegroundAsync(request);
                request.Busy = false;
                request.NeedsForegroundSync = false;
                request.ResumeOnForeground = false;
                SetAudioPaused(request, false);
                m_player.ResumeVideo();
                trace.Complete();
            }
            catch (Exception exception)
            {
                trace.Fail(exception);
                if (ReferenceEquals(m_request, request) && !(exception is OperationCanceledException)) Fail(exception);
                throw;
            }
        }

        private async UniTask StartAudioAsync(Request request)
        {
            if (request.AudioScope == null) return;
            request.PlayID = await AudioManager.Instance.PlayAudioAsync(request.AudioKey,
                new AudioPlayOptions
                {
                    Scope = request.AudioScope,
                    ReadinessMode = EAudioReadinessMode.RequireReady,
                    StartTimeMs = ToMilliseconds(m_player.GetPlayTimeMs() / 1000d)
                },
                request.Token);
            request.AudioEnded = false;
            request.AudioPaused = false;
            if (m_applicationPaused) SetAudioPaused(request, true);
            request.NextSyncTime = Time.realtimeSinceStartupAsDouble + 0.5;
        }

        private async UniTask WaitForForegroundAsync(Request request)
        {
            // 准备和循环跨过切后台事件时，也不能在后台启动新音轨或视频时钟。
            EnsureCurrent(request);
            while (m_applicationPaused)
            {
                await UniTask.NextFrame(request.Token);
                EnsureCurrent(request);
            }
        }

        private async UniTask RecoverForegroundAsync(Request request)
        {
            request.Recovering = true;
            request.NeedsForegroundSync = false;
            double position = m_player.PositionSeconds;
            try
            {
                // 复用拖动进度条的定位链：先提交目标画面，再把 Wwise 定位到同一时间。
                await SeekAsync(position, request.Token);
                EnsureCurrent(request);
                if (m_applicationPaused || request.NeedsForegroundSync) return;
                bool resume = request.ResumeOnForeground;
                request.ResumeOnForeground = false;
                if (resume)
                {
                    SetAudioPaused(request, false);
                    m_player.ResumeVideo();
                }
                Debug.Log($"[VideoLifecycle] 前台音画同步完成，位置={position:F3}s，继续播放={resume}。");
            }
            catch (Exception exception)
            {
                // 关闭或换片后，旧恢复任务不能影响新会话；原生恢复失败则停止音画。
                if (ReferenceEquals(m_request, request) && !(exception is OperationCanceledException))
                    Fail(exception);
                throw;
            }
            finally { request.Recovering = false; }
        }

        private static async UniTask RetireAsync(Request request, UniTask videoClosed,
            UniTaskCompletionSource completion)
        {
            try
            {
                try
                {
                    await request.Prepared.Task;
                    await videoClosed;
                    request.Video?.Dispose();
                    request.Subtitles = null;
                }
                finally { request.Cancellation.Dispose(); }
                request.CloseTrace.Complete();
                completion.TrySetResult();
            }
            catch (OperationCanceledException exception)
            {
                request.CloseTrace.Fail(exception);
                completion.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                request.CloseTrace.Fail(exception);
                completion.TrySetException(exception);
            }
        }

        private static void SetAudioPaused(Request request, bool paused)
        {
            // 同一实例只发送成对的暂停与恢复，避免重复 Seek 或暂停后 Seek 叠加原生暂停计数。
            if (!IsAudioActive(request) || request.AudioPaused == paused) return;
            AudioManager audio = AudioManager.Instance;
            bool accepted = paused ? audio.PauseAudio(request.PlayID) : audio.ResumeAudio(request.PlayID);
            if (!accepted) throw new InvalidOperationException(paused ? "音轨暂停失败。" : "音轨恢复失败。");
            request.AudioPaused = paused;
        }

        private Request RequireActive()
        {
            if (m_isDisposed) throw new ObjectDisposedException(nameof(VideoPlaybackCoordinator));
            if (m_request == null) throw new InvalidOperationException("没有活跃的视频请求。");
            return m_request;
        }

        private static bool IsAudioActive(Request request)
        {
            if (request.PlayID == 0 || request.AudioEnded) return false;
            AudioManager audio = AudioManager.Instance;
            if (!audio.IsReady) throw new InvalidOperationException("音频后端已关闭。");
            if (audio.TryGetPlayback(request.PlayID, out AudioPlaybackInfo info))
            {
                if (info.State == EAudioPlaybackState.Ended) { request.AudioEnded = true; return false; }
                if (info.State == EAudioPlaybackState.Failed) throw new InvalidOperationException(info.Result.Message);
            }
            return true;
        }

        private void EnsureCurrent(Request request)
        {
            request.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(m_request, request) || m_isDisposed || m_player == null ||
                !m_player.isActiveAndEnabled)
                throw new OperationCanceledException(request.Token);
        }

        private void Fail(Exception exception)
        {
            CloseAsync().Forget(LogFailure);
            if (m_player != null) m_player.ReportPlaybackFailure(exception);
        }

        private static int ToMilliseconds(double seconds)
        {
            if (seconds > int.MaxValue / 1000d)
                throw new InvalidOperationException("视频时长超出当前 Wwise 毫秒时间轴范围。");
            return (int)Math.Max(0, seconds * 1000);
        }

        private static void LogFailure(Exception exception)
        {
            if (!(exception is OperationCanceledException)) Debug.LogException(exception);
        }

        private sealed class Request : IProgress<float>
        {
            private readonly VideoPlaybackCoordinator m_owner; // 接收当前请求下载进度的宿主。

            internal CancellationTokenSource Cancellation { get; }
            internal CancellationToken Token { get; }
            internal UniTaskCompletionSource Prepared { get; } = new UniTaskCompletionSource();
            internal VideoResourceLease Video { get; set; }
            internal PulseSubtitleTrack Subtitles { get; set; }
            internal VideoPlaybackTrace CloseTrace { get; set; }
            internal AudioScope AudioScope { get; set; }
            internal string AudioKey { get; }
            internal EPulseLoopMode LoopMode { get; }
            internal int PlayCount { get; }
            internal long PlayID { get; set; }
            internal int CompletedCount { get; set; }
            internal bool Busy { get; set; } = true;
            internal bool Seeking { get; set; }
            internal bool ResumeAfterSeek { get; set; }
            internal int SeekGeneration { get; set; }
            internal bool AudioEnded { get; set; }
            internal bool AudioPaused { get; set; }
            internal bool NeedsForegroundSync { get; set; }
            internal bool ResumeOnForeground { get; set; }
            internal bool Recovering { get; set; }
            internal double NextSyncTime { get; set; }

            internal Request(VideoPlaybackCoordinator owner, CancellationToken token, string audioKey,
                EPulseLoopMode loopMode, int playCount)
            {
                m_owner = owner;
                Cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
                Token = Cancellation.Token;
                AudioKey = audioKey;
                LoopMode = loopMode;
                PlayCount = playCount;
            }

            /// <summary>把当前有效请求的下载进度映射到整体准备进度的前 60%。</summary>
            /// <param name="value">下载进度，范围零至一。</param>
            public void Report(float value)
            {
                if (ReferenceEquals(m_owner.m_request, this) && !Token.IsCancellationRequested &&
                    m_owner.m_player != null)
                    m_owner.m_player.SetLoadingProgress(Mathf.Clamp01(value) * 0.6f);
            }
        }
    }
}
