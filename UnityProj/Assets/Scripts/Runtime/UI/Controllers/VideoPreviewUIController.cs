using System;
using System.Threading;
using AlloyFramework;
using AlloyFramework.UI;
using Cysharp.Threading.Tasks;
using Game.Video;

namespace Game.UI
{
    /// <summary>管理视频预览界面的播放器关闭和资源释放。</summary>
    public sealed class VideoPreviewUIController : UIController<VideoPreviewUIView>
    {
        private const string VIDEOLOCATION = "Video/Synthetic/Sintel.2010.1080p"; // VideoPackage 中的视频资源地址。
        private const string AUDIOEVENTKEY = "Play_Sintel"; // Wwise 中为此视频制作的专用音轨事件。
        private const string SUBTITLELOCATION = "Video/Synthetic/Sintel.2010.1080p.en"; // 本地联调英文字幕地址。
        private GameVideoPlayer m_videoPlayer; // 预制体上封装资源和音轨的工程视频组件。
        private UniTask m_playerCloseTask; // 可重复等待的关闭任务，供关闭动画和销毁流程共享。
        private bool m_isClosingPlayer; // 本轮关闭已发起，避免重复提交资源释放请求。

        // Controller 和 View 首次创建后调用一次。
        protected override void OnCreate()
        {
            // 获取已有组件，不能通过 new 创建 MonoBehaviour。
            m_videoPlayer = View.GetComponentInChildren<GameVideoPlayer>(true);
            if (m_videoPlayer == null)
                throw new InvalidOperationException("VideoPreviewUI 预制体缺少 GameVideoPlayer 组件。");
            View.BtnClose.onClick.AddListener(OnCloseBtnClick);
        }

        private void OnCloseBtnClick()
        {
            Close();
        }

        // 每次打开前调用，可以异步准备数据和资源。
        protected override UniTask OnPrepareAsync(
            UIEmptyData data, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        // 每次打开时初始化界面数据。
        protected override void OnInitData(UIEmptyData data)
        {
            // 缓存界面再次打开时恢复组件监听，是否自动播放仍由组件配置决定。
            m_isClosingPlayer = false;
            m_playerCloseTask = UniTask.CompletedTask;
            if (m_videoPlayer != null) m_videoPlayer.enabled = true;
        }

        // 已打开的界面以刷新模式再次打开时调用。
        protected override void OnRefresh(UIEmptyData data)
        {
        }

        // 开始播放打开动画前调用。
        protected override void OnStartOpenAnimation()
        {
        }

        // 返回打开动画任务；没有动画时直接返回已完成任务。
        protected override UniTask OnOpenAnimationAsync(
            CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        // 打开动画执行完成后调用。
        protected override void OnEndOpenAnimation()
        {
            // 资源地址由 VideoPackage 解析；音轨使用事件名，由 AudioPackage 准备 Wwise 依赖。
            m_videoPlayer.Play(VIDEOLOCATION, AUDIOEVENTKEY, SUBTITLELOCATION, startTimeSeconds: 4.0);
        }

        // 界面完成打开并可以交互后调用。
        protected override void OnOpen()
        {
        }

        // 界面被更高层界面暂停时调用。
        protected override void OnPause()
        {
        }

        // 界面从暂停状态恢复时调用。
        protected override void OnResume()
        {
        }

        // 界面开始关闭时调用。
        protected override void OnClose()
        {
            BeginPlayerClose();
        }

        // 开始播放关闭动画前调用。
        protected override void OnStartCloseAnimation()
        {
        }

        // 关闭动画阶段同时等待视频资源退役。
        protected override UniTask OnCloseAnimationAsync(
            CancellationToken cancellationToken)
        {
            // 与界面动画并行等待释放；不因动画取消而放弃原生资源清理。
            BeginPlayerClose();
            return m_playerCloseTask;
        }

        // 关闭动画执行完成后调用。
        protected override void OnEndCloseAnimation()
        {
        }

        // Controller 被彻底释放时调用。
        protected override void OnDispose()
        {
            // 打开失败或直接销毁时可能未经过 OnClose，仍需请求释放且不再访问 View。
            BeginPlayerClose();
            m_playerCloseTask.Forget(LogPlayerCloseFailure);
            m_videoPlayer = null;
        }

        private void BeginPlayerClose()
        {
            if (m_isClosingPlayer) return;
            m_isClosingPlayer = true;
            m_playerCloseTask = UniTask.CompletedTask;
            if (m_videoPlayer == null) return;

            // 先解除视频绑定并取消未完成的打开，再禁用组件以移除按钮和滑条监听。
            // 工程组件返回的关闭完成源已支持并发等待，不再额外包裹 Preserve。
            m_playerCloseTask = m_videoPlayer.CloseAsync();
            m_videoPlayer.enabled = false;
        }

        private static void LogPlayerCloseFailure(Exception exception)
        {
            AlloyDebug.Error(exception);
        }
    }
}
