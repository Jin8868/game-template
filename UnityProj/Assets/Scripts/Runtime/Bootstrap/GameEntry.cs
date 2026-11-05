using System;
using System.Threading;
using AlloyFramework;
using AlloyFramework.UI;
using Cysharp.Threading.Tasks;
using Game.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace Game
{
    /// <summary>统一协调业务模块初始化、进入游戏和逆序退出。</summary>
    [Preserve]
    public sealed class GameEntry : IGameEntry
    {
        private const string SHUTDOWNSCENENAME = "AlloyFrameworkShutdown"; // 卸载最后一个场景时的承接场景。
        private const float SCENELOADWEIGHT = 0.9f; // 入口内部为首页就绪预留最后一成进度。
        private readonly GameStartupPipeline m_startupPipeline = new GameStartupPipeline(); // 可扩展的业务模块管线。

        private ISceneHandle m_sceneHandle; // 游戏入口持有的初始业务场景句柄。
        private UIHandle m_homeHandle; // 退出业务模块前需要关闭的首页。
        private IStartupLoadingSession m_loadingSession; // 框架持有的共享会话，入口仅报告内部进度。
        private bool m_hasInitializationStarted; // 初始化失败后也不允许复用同一入口重试。
        private bool m_isInitialized; // 所有业务模块均已完成初始化。
        private bool m_hasStarted; // 防止重复执行游戏入口。

        /// <summary>通过可扩展业务管线安装项目模块，完成后才允许进入游戏。</summary>
        /// <param name="loadingSession">用于报告当前初始化操作的共享展示会话。</param>
        /// <param name="cancellationToken">用于取消业务初始化的令牌。</param>
        /// <returns>全部业务模块就绪的任务。</returns>
        /// <exception cref="ArgumentNullException">展示会话为空。</exception>
        /// <exception cref="InvalidOperationException">初始化重复执行。</exception>
        public async UniTask InitializeAsync(
            IStartupLoadingSession loadingSession,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (loadingSession == null) throw new ArgumentNullException(nameof(loadingSession));
            if (m_hasInitializationStarted) throw new InvalidOperationException("业务初始化不能重复执行。");

            // 入口只协调生命周期，新增模块仍在业务管线中通过 Add 注册。
            m_hasInitializationStarted = true;
            await m_startupPipeline.RunAsync(loadingSession, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            m_isInitialized = true;
        }

        /// <summary>加载初始业务场景并等待首页就绪，入口整体由框架计为一个步骤。</summary>
        /// <param name="loadingSession">框架已开始游戏入口步骤的共享会话，仅用于报告内部进度。</param>
        /// <param name="cancellationToken">用于取消进入游戏流程的令牌。</param>
        /// <returns>首个业务界面就绪的任务。</returns>
        /// <exception cref="ArgumentNullException">共享的 Loading 会话为空。</exception>
        /// <exception cref="InvalidOperationException">业务尚未初始化、入口重复执行或游戏首页尚未就绪。</exception>
        public async UniTask MainAsync(
            IStartupLoadingSession loadingSession,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (loadingSession == null)
            {
                throw new ArgumentNullException(nameof(loadingSession));
            }

            if (!m_isInitialized || m_hasStarted)
            {
                throw new InvalidOperationException("游戏入口需要业务初始化完成，且不能重复执行。");
            }

            m_hasStarted = true;
            m_loadingSession = loadingSession;

            // 只报告入口内部进度，步骤完成和展示关闭统一由框架负责。
            try
            {
                await LoadInitialSceneAsync(cancellationToken);
                await EnterInitialUIAsync(cancellationToken);
            }
            finally
            {
                m_loadingSession = null;
            }
        }

        /// <summary>依次关闭首页、卸载入口场景，并逆序退出已开始的业务模块。</summary>
        /// <returns>业务资源退出清理完成的任务。</returns>
        public async UniTask ShutdownAsync()
        {
            // 共享会话由框架释放，入口只解除引用并回收自己持有的场景。
            m_loadingSession = null;
            m_isInitialized = false;
            var homeHandle = m_homeHandle;
            m_homeHandle = null;
            var sceneHandle = m_sceneHandle;
            m_sceneHandle = null;

            // 每层退出失败仍须继续释放其依赖，尤其要清理初始化中途失败的模块。
            try
            {
                if (homeHandle != null) await homeHandle.CloseAsync();
            }
            finally
            {
                try
                {
                    if (sceneHandle != null && !sceneHandle.IsUnloaded)
                    {
                        EnsureSceneCanUnload(sceneHandle);
                        await sceneHandle.UnloadAsync();
                    }
                }
                finally
                {
                    await m_startupPipeline.ShutdownAsync();
                }
            }
        }

        private async UniTask LoadInitialSceneAsync(CancellationToken cancellationToken)
        {
            // 场景占入口进度的前九成，剩余进度等待首页就绪。
            m_sceneHandle = await ResourceManager.Instance.LoadSceneAsync(
                "Scenes/SampleScene",
                OnSceneLoadProgress,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            m_loadingSession?.Report(SCENELOADWEIGHT);
        }

        private async UniTask EnterInitialUIAsync(CancellationToken cancellationToken)
        {
            // 首页完成导航并获得一帧显示机会后才返回，由框架结束入口步骤并关闭展示。
            m_homeHandle = await UIManager.Instance.JumpAsync(
                NavigationJumpID.HOME,
                cancellationToken: cancellationToken);
            await UniTask.NextFrame(cancellationToken);
            if (m_homeHandle == null || !m_homeHandle.IsValid || m_homeHandle.State != UIState.Active)
            {
                throw new InvalidOperationException("游戏首页尚未就绪，不能关闭启动 Loading。");
            }
        }

        private static void EnsureSceneCanUnload(ISceneHandle sceneHandle)
        {
            if (SceneManager.sceneCount != 1 ||
                !sceneHandle.Scene.IsValid() ||
                !sceneHandle.Scene.isLoaded)
            {
                return;
            }

            // Unity 不允许卸载最后一个场景，先建立空场景承接关闭阶段。
            var shutdownScene = SceneManager.CreateScene(SHUTDOWNSCENENAME);
            SceneManager.SetActiveScene(shutdownScene);
        }

        private void OnSceneLoadProgress(float progress)
        {
            m_loadingSession?.Report(progress * SCENELOADWEIGHT);
        }
    }
}
