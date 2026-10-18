using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PulseVideoPlayer;
using UnityEngine;

namespace Game.Video
{
    /// <summary>工程侧视频组件，统一封装 YooAssets、CDN 视频和 Wwise 音轨的播放生命周期。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PulseVideoPlayer.UI.PulseVideoPlayer))]
    [DefaultExecutionOrder(-100)]
    public sealed class GameVideoPlayer : MonoBehaviour
    {
        [SerializeField] private string m_packageName = "VideoPackage"; // 视频原始文件所在的资源包。
        private PulseVideoPlayer.UI.PulseVideoPlayer m_player; // 同物体上负责画面和 UI 交互的组件。
        private VideoPlaybackCoordinator m_coordinator; // 本组件独占的资源和音画协调器。
        private bool m_isDisposed; // 销毁后禁止重新初始化协调器。
        private bool m_applicationPaused; // 应用暂停信号与桌面焦点信号合并后通知协调器。
        private bool m_applicationFocused = true; // Windows 切换到其他窗口时暂停视频。

        /// <summary>当前视频状态。</summary>
        public EPulsePlayerState State => m_player != null ? m_player.State : EPulsePlayerState.Closed;
        /// <summary>是否正在加载或准备视频。</summary>
        public bool IsLoading => m_player != null && m_player.IsLoading;
        /// <summary>当前加载进度，范围零至一。</summary>
        public float LoadingProgress => m_player != null ? m_player.LoadingProgress : 0;
        /// <summary>是否正在准备、跳转或切换循环。</summary>
        public bool IsBusy => m_coordinator?.IsBusy ?? false;
        /// <summary>当前视频总时长，单位为秒。</summary>
        public double DurationSeconds => m_player != null ? m_player.DurationSeconds : 0;
        /// <summary>最近一次播放错误。</summary>
        public Exception LastError => m_player != null ? m_player.LastError : null;
        /// <summary>底层 PulseVideoPlayer 在 Inspector 绑定的字幕 Text。</summary>
        public UnityEngine.UI.Text SubtitleText => m_player != null ? m_player.SubtitleText : null;

        /// <summary>当前是否正在播放，准备和跳转时为 false。</summary>
        public bool IsPlaying => m_player != null && m_player.IsPlaying;
        /// <summary>当前是否暂停。</summary>
        public bool IsPaused => m_player != null && m_player.IsPaused;
        /// <summary>是否正在等待定位目标画面。</summary>
        public bool IsSeeking => m_player != null && m_player.IsSeeking;
        /// <summary>当前播放位置，单位为秒。</summary>
        public double PositionSeconds => m_player != null ? m_player.PositionSeconds : 0;
        /// <summary>当前播放进度，范围零至一。</summary>
        public double NormalizedProgress => m_player != null ? m_player.NormalizedProgress : 0;
        /// <summary>当前是否允许拖动或定位。</summary>
        public bool CanSeek => isActiveAndEnabled && !IsBusy && !IsLoading && DurationSeconds > 0 &&
            (State == EPulsePlayerState.Ready || State == EPulsePlayerState.Playing ||
            State == EPulsePlayerState.Paused || State == EPulsePlayerState.Ended);
        /// <summary>当前是否允许暂停。</summary>
        public bool CanPause => isActiveAndEnabled && !IsBusy && State == EPulsePlayerState.Playing;
        /// <summary>当前是否允许恢复。</summary>
        public bool CanResume => isActiveAndEnabled && !IsBusy && State == EPulsePlayerState.Paused;
        /// <summary>首帧准备完成。</summary>
        public event Action Ready;
        /// <summary>最后一轮播放结束，无限循环不会触发。</summary>
        public event Action Completed;
        /// <summary>视频或音轨准备、播放失败。</summary>
        public event Action<Exception> Failed;

        /// <summary>视频时间轴开始推进，音轨已提交。</summary>
        public event Action Started;
        /// <summary>播放已暂停。</summary>
        public event Action Paused;
        /// <summary>播放已恢复。</summary>
        public event Action Resumed;
        /// <summary>显示已解绑并请求关闭，不代表原生资源已经释放完成。</summary>
        public event Action Stopped;
        /// <summary>加载进度变化，参数范围零至一。</summary>
        public event Action<float> LoadingProgressChanged;
        private void Awake()
        {
            // 先于底层组件的自动播放注入宿主实现，业务无需手动创建协调器。
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            m_player.enabled = true;
            m_applicationFocused = Application.isFocused;
            SynchronizeApplicationState();
        }

        private void OnApplicationPause(bool paused)
        {
            // 使用应用暂停事件同步音画，不能只依赖 Unity 停止绘制视频。
            m_applicationPaused = paused;
            SynchronizeApplicationState();
        }

        private void OnApplicationFocus(bool focused)
        {
            m_applicationFocused = focused;
            SynchronizeApplicationState();
        }

        private void SynchronizeApplicationState()
        {
            bool paused = m_applicationPaused;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // Windows 切后台主要表现为失去焦点；Android 软键盘也会影响焦点，使用暂停信号。
            paused |= !m_applicationFocused;
#endif
            m_coordinator?.SetApplicationPaused(paused);
        }

        /// <summary>使用 Inspector 配置播放，已有请求暂停时恢复，结束时重新播放。</summary>
        /// <exception cref="InvalidOperationException">组件未启用。</exception>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public void Play()
        {
            RequireActivePlayer().Play();
        }

        /// <summary>加载并播放视频，可选 Wwise 音轨，失败通过 Failed 和日志报告。</summary>
        /// <param name="videoLocation">YooAssets 地址、本地文件路径或 HTTP/HTTPS 视频 URL。</param>
        /// <param name="audioEventKey">Wwise 事件名，空值表示无音轨。</param>
        /// <param name="loopMode">播放一次、无限循环或指定总次数。</param>
        /// <param name="playCount">Count 模式的总播放次数，包含第一次。</param>
        /// <exception cref="InvalidOperationException">组件未启用。</exception>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public void Play(string videoLocation, string audioEventKey = null,
            EPulseLoopMode loopMode = EPulseLoopMode.Once, int playCount = 1)
        {
            RequireActivePlayer().Play(videoLocation, audioEventKey, loopMode, playCount);
        }

        /// <summary>加载并开始播放，任务完成表示音画已启动，不等待视频结束。</summary>
        /// <param name="videoLocation">YooAssets 地址、本地文件路径或 HTTP/HTTPS 视频 URL。</param>
        /// <param name="audioEventKey">Wwise 事件名，空值表示无音轨。</param>
        /// <param name="loopMode">播放一次、无限循环或指定总次数。</param>
        /// <param name="playCount">Count 模式的总播放次数，必须大于零。</param>
        /// <param name="cancellationToken">取消本次播放生命周期。</param>
        /// <returns>视频与可选音轨已经开始播放的任务。</returns>
        /// <exception cref="ArgumentException">视频地址为空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">循环策略或次数无效。</exception>
        /// <exception cref="InvalidOperationException">组件未启用或音频系统未就绪。</exception>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public UniTask PlayAsync(string videoLocation, string audioEventKey = null,
            EPulseLoopMode loopMode = EPulseLoopMode.Once, int playCount = 1,
            CancellationToken cancellationToken = default)
        {
            return RequireActivePlayer().PlayAsync(
                videoLocation, audioEventKey, loopMode, playCount, cancellationToken);
        }

        /// <summary>加载视频、外置 SRT 和可选音轨，错误通过 Failed 和日志报告。</summary>
        /// <param name="videoLocation">视频资源地址、本地路径或 URL。</param>
        /// <param name="audioEventKey">Wwise 音轨事件名。</param>
        /// <param name="subtitleLocation">字幕资源地址、本地 SRT 路径或 URL。</param>
        /// <param name="loopMode">循环模式。</param>
        /// <param name="playCount">Count 模式的总播放次数。</param>
        /// <exception cref="InvalidOperationException">组件未启用。</exception>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public void Play(string videoLocation, string audioEventKey, string subtitleLocation,
            EPulseLoopMode loopMode = EPulseLoopMode.Once, int playCount = 1)
        {
            RequireActivePlayer().Play(
                videoLocation, audioEventKey, subtitleLocation, loopMode, playCount);
        }

        /// <summary>等待视频、外置 SRT 和可选音轨就绪并开始播放。</summary>
        /// <param name="videoLocation">视频资源地址、本地路径或 URL。</param>
        /// <param name="audioEventKey">Wwise 音轨事件名，空值表示无音轨。</param>
        /// <param name="subtitleLocation">DefaultPackage 字幕地址、本地 SRT 路径或 URL。</param>
        /// <param name="loopMode">循环模式。</param>
        /// <param name="playCount">Count 模式的总播放次数。</param>
        /// <param name="cancellationToken">播放生命周期令牌。</param>
        /// <returns>字幕准备完成且音画已启动的任务。</returns>
        /// <exception cref="ArgumentException">视频或字幕地址为空。</exception>
        /// <exception cref="FormatException">字幕格式无效。</exception>
        /// <exception cref="ArgumentOutOfRangeException">循环策略或次数无效。</exception>
        /// <exception cref="InvalidOperationException">组件未启用或资源、音频系统未就绪。</exception>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public UniTask PlayAsync(string videoLocation, string audioEventKey, string subtitleLocation,
            EPulseLoopMode loopMode = EPulseLoopMode.Once, int playCount = 1,
            CancellationToken cancellationToken = default)
        {
            return RequireActivePlayer().PlayAsync(
                videoLocation, audioEventKey, subtitleLocation, loopMode, playCount, cancellationToken);
        }

        /// <summary>从指定起点播放视频和可选音轨，后续循环仍从零开始。</summary>
        /// <param name="videoLocation">视频资源地址、本地路径或 URL。</param>
        /// <param name="audioEventKey">可选音轨事件，无音轨传 null。</param>
        /// <param name="startTimeSeconds">非负起点秒数，超出时长定位末帧。</param>
        /// <param name="loopMode">循环策略。</param>
        /// <param name="playCount">指定总次数。</param>
        /// <exception cref="InvalidOperationException">组件未启用。</exception>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public void Play(string videoLocation, string audioEventKey, double startTimeSeconds,
            EPulseLoopMode loopMode = EPulseLoopMode.Once, int playCount = 1)
        {
            RequireActivePlayer().Play(videoLocation, audioEventKey, startTimeSeconds, loopMode, playCount);
        }

        /// <summary>从指定起点播放视频、可选音轨和可选字幕。</summary>
        /// <param name="videoLocation">视频资源地址、本地路径或 URL。</param>
        /// <param name="audioEventKey">可选音轨事件。</param>
        /// <param name="subtitleLocation">可选字幕地址。</param>
        /// <param name="startTimeSeconds">首次起点秒数，后续循环从零开始。</param>
        /// <param name="loopMode">循环模式。</param>
        /// <param name="playCount">指定总次数。</param>
        /// <exception cref="InvalidOperationException">组件未启用。</exception>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public void Play(string videoLocation, string audioEventKey, string subtitleLocation,
            double startTimeSeconds, EPulseLoopMode loopMode = EPulseLoopMode.Once, int playCount = 1)
        {
            RequireActivePlayer().Play(videoLocation, audioEventKey, subtitleLocation,
                startTimeSeconds, loopMode, playCount);
        }

        /// <summary>等待视频和音轨从指定起点开始播放。</summary>
        /// <param name="videoLocation">视频资源地址、本地路径或 URL。</param>
        /// <param name="audioEventKey">可选音轨事件。</param>
        /// <param name="startTimeSeconds">非负起点秒数。</param>
        /// <param name="loopMode">循环模式。</param>
        /// <param name="playCount">指定总次数。</param>
        /// <param name="cancellationToken">播放生命周期令牌。</param>
        /// <returns>目标画面和音轨就绪并开始播放的任务。</returns>
        /// <exception cref="ArgumentOutOfRangeException">起点或循环参数无效。</exception>
        /// <exception cref="InvalidOperationException">组件或音频未就绪。</exception>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public UniTask PlayAsync(string videoLocation, string audioEventKey, double startTimeSeconds,
            EPulseLoopMode loopMode = EPulseLoopMode.Once, int playCount = 1,
            CancellationToken cancellationToken = default)
        {
            return RequireActivePlayer().PlayAsync(videoLocation, audioEventKey, startTimeSeconds,
                loopMode, playCount, cancellationToken);
        }

        /// <summary>等待视频、音轨和字幕从指定起点开始播放。</summary>
        /// <param name="videoLocation">视频资源地址、本地路径或 URL。</param>
        /// <param name="audioEventKey">可选音轨事件。</param>
        /// <param name="subtitleLocation">可选字幕地址。</param>
        /// <param name="startTimeSeconds">非负起点秒数。</param>
        /// <param name="loopMode">循环模式。</param>
        /// <param name="playCount">指定总次数。</param>
        /// <param name="cancellationToken">播放生命周期令牌。</param>
        /// <returns>目标画面和音轨就绪并开始播放的任务。</returns>
        /// <exception cref="ArgumentOutOfRangeException">起点或循环参数无效。</exception>
        /// <exception cref="InvalidOperationException">组件或音频未就绪。</exception>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public UniTask PlayAsync(string videoLocation, string audioEventKey, string subtitleLocation,
            double startTimeSeconds, EPulseLoopMode loopMode = EPulseLoopMode.Once, int playCount = 1,
            CancellationToken cancellationToken = default)
        {
            return RequireActivePlayer().PlayAsync(videoLocation, audioEventKey, subtitleLocation,
                startTimeSeconds, loopMode, playCount, cancellationToken);
        }

        /// <summary>等待最近的视频从零重播，保留音轨、字幕和循环配置，重置播放轮次。</summary>
        /// <param name="cancellationToken">新播放请求的生命周期令牌。</param>
        /// <returns>重播已经启动的任务。</returns>
        /// <exception cref="InvalidOperationException">组件未启用。</exception>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public UniTask ReplayAsync(CancellationToken cancellationToken = default)
        {
            return RequireActivePlayer().ReplayAsync(cancellationToken);
        }

        /// <summary>在播放与暂停之间切换，不在其他状态隐式发起新播放。</summary>
        /// <exception cref="InvalidOperationException">当前未处于播放或暂停状态，或仍在准备。</exception>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public void TogglePause() { RequireActivePlayer().TogglePause(); }

        /// <summary>按零至一的进度定位音画，超出范围会钳制。</summary>
        /// <param name="progress">目标进度，例如 0.5 表示一半。</param>
        /// <exception cref="ArgumentOutOfRangeException">进度不是有限值。</exception>
        /// <exception cref="InvalidOperationException">视频未就绪。</exception>
        public void SeekNormalized(double progress) { RequireActivePlayer().SeekNormalized(progress); }

        /// <summary>等待按零至一的进度定位音画。</summary>
        /// <param name="progress">目标进度，超出范围会钳制。</param>
        /// <param name="cancellationToken">定位取消令牌。</param>
        /// <returns>音画定位完成的任务。</returns>
        /// <exception cref="ArgumentOutOfRangeException">进度不是有限值。</exception>
        /// <exception cref="InvalidOperationException">视频未就绪。</exception>
        public UniTask SeekNormalizedAsync(double progress, CancellationToken cancellationToken = default)
        {
            return RequireActivePlayer().SeekNormalizedAsync(progress, cancellationToken);
        }


        /// <summary>切换字幕显示，重新开启时立即对齐当前视频时间，不重新加载文件。</summary>
        /// <param name="enabled">是否显示字幕。</param>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public void SetSubtitlesEnabled(bool enabled)
        {
            EnsureInitialized();
            m_player.SetSubtitlesEnabled(enabled);
        }

        /// <summary>同时暂停视频和音轨。</summary>
        /// <exception cref="InvalidOperationException">组件未启用、没有请求或仍在准备。</exception>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public void Pause()
        {
            RequireActivePlayer().Pause();
        }

        /// <summary>同时恢复视频和音轨。</summary>
        /// <exception cref="InvalidOperationException">组件未启用、没有请求或仍在准备。</exception>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public void Resume()
        {
            RequireActivePlayer().Resume();
        }

        /// <summary>使用最近一次请求的地址、音轨和循环参数重新播放。</summary>
        /// <exception cref="InvalidOperationException">组件未启用。</exception>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public void Replay()
        {
            RequireActivePlayer().Replay();
        }

        /// <summary>将视频和音轨跳转到指定秒数，失败通过 Failed 和日志报告。</summary>
        /// <param name="timeSeconds">目标时间，超出时长会钳制到有效范围。</param>
        /// <exception cref="InvalidOperationException">组件未启用。</exception>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public void Seek(double timeSeconds)
        {
            RequireActivePlayer().Seek(timeSeconds);
        }

        /// <summary>等待视频和音轨定位完成，恢复跳转前的播放或暂停状态。</summary>
        /// <param name="timeSeconds">目标秒数。</param>
        /// <param name="cancellationToken">取消当前跳转会关闭本次播放会话。</param>
        /// <returns>音画定位完成的任务。</returns>
        /// <exception cref="ArgumentOutOfRangeException">时间不是有限数值。</exception>
        /// <exception cref="InvalidOperationException">组件未启用、没有请求或仍在准备。</exception>
        /// <exception cref="ObjectDisposedException">组件已经销毁。</exception>
        public UniTask SeekAsync(double timeSeconds, CancellationToken cancellationToken = default)
        {
            return RequireActivePlayer().SeekAsync(timeSeconds, cancellationToken);
        }

        /// <summary>读取当前视频播放位置。</summary>
        /// <returns>播放时间，单位为毫秒，未播放时为零。</returns>
        public double GetPlayTimeMs()
        {
            return m_player != null ? m_player.GetPlayTimeMs() : 0;
        }

        /// <summary>立即请求停止音画，资源释放在后台完成，可再次调用 Play。</summary>
        public void Close()
        {
            CloseAsync().Forget(LogCloseFailure);
        }

        /// <summary>关闭音画，并等待原生视频退役后释放文件与下载资源。</summary>
        /// <returns>当前请求的清理任务，未初始化时直接完成。</returns>
        public UniTask CloseAsync()
        {
            return m_coordinator?.CloseAsync() ?? UniTask.CompletedTask;
        }

        private void EnsureInitialized()
        {
            if (m_isDisposed) throw new ObjectDisposedException(nameof(GameVideoPlayer));
            if (m_coordinator != null) return;

            // 保留原组件的序列化 UI 引用，只安装工程适配层。
            m_player = GetComponent<PulseVideoPlayer.UI.PulseVideoPlayer>();
            if (m_player == null) throw new InvalidOperationException("缺少同物体上的 PulseVideoPlayer 组件。");
            var coordinator = new VideoPlaybackCoordinator(m_player, m_packageName);
            m_player.SetPlaybackDriver(coordinator);
            m_coordinator = coordinator;
            m_player.Ready += OnReady;
            m_player.Started += OnStarted;
            m_player.Paused += OnPaused;
            m_player.Resumed += OnResumed;
            m_player.Stopped += OnStopped;
            m_player.LoadingProgressChanged += OnLoadingProgressChanged;
            m_player.Completed += OnCompleted;
            m_player.Failed += OnFailed;
        }

        private PulseVideoPlayer.UI.PulseVideoPlayer RequireActivePlayer()
        {
            EnsureInitialized();
            if (!isActiveAndEnabled || !m_player.isActiveAndEnabled)
                throw new InvalidOperationException("请先启用 GameVideoPlayer 及其所在物体。");
            return m_player;
        }

        private void OnStarted() { Started?.Invoke(); }
        private void OnPaused() { Paused?.Invoke(); }
        private void OnResumed() { Resumed?.Invoke(); }
        private void OnStopped() { Stopped?.Invoke(); }
        private void OnLoadingProgressChanged(float progress) { LoadingProgressChanged?.Invoke(progress); }
        private void OnReady() { Ready?.Invoke(); }
        private void OnCompleted() { Completed?.Invoke(); }
        private void OnFailed(Exception exception) { Failed?.Invoke(exception); }

        private void OnDisable()
        {
            // 同时移除底层按钮和滑条监听，隐藏或禁用时不能继续播放。
            Close();
            if (m_player != null) m_player.enabled = false;
        }

        private void OnDestroy()
        {
            m_isDisposed = true;
            if (m_player != null)
            {
                m_player.Ready -= OnReady;
                m_player.Started -= OnStarted;
                m_player.Paused -= OnPaused;
                m_player.Resumed -= OnResumed;
                m_player.Stopped -= OnStopped;
                m_player.LoadingProgressChanged -= OnLoadingProgressChanged;
                m_player.Completed -= OnCompleted;
                m_player.Failed -= OnFailed;
            }
            // 协调器等待在途准备和原生关闭后才释放文件，不同步阻塞 Unity 主线程。
            m_coordinator?.Dispose();
            m_coordinator = null;
        }

        private static void LogCloseFailure(Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
