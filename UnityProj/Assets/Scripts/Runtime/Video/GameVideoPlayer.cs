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

        /// <summary>首帧准备完成。</summary>
        public event Action Ready;
        /// <summary>最后一轮播放结束，无限循环不会触发。</summary>
        public event Action Completed;
        /// <summary>视频或音轨准备、播放失败。</summary>
        public event Action<Exception> Failed;

        private void Awake()
        {
            // 先于底层组件的自动播放注入宿主实现，业务无需手动创建协调器。
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            m_player.enabled = true;
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
