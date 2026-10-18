using System;
using System.Threading;
using AlloyFramework;
using AlloyFramework.Audio;
using AlloyFramework.UI;
using Cysharp.Threading.Tasks;
using Game.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace Game
{
    /// <summary>业务初始化完成后进入游戏，负责初始场景、首页和成功关闭 Loading。</summary>
    [Preserve]
    public sealed class GameEntry : IGameEntry
    {
        private const string SHUTDOWNSCENENAME = "AlloyFrameworkShutdown"; // 卸载最后一个场景时的承接场景。

        private ISceneHandle m_sceneHandle; // 游戏入口持有的初始业务场景句柄。
        private IStartupLoadingSession m_loadingSession; // 与业务初始化共享的 Loading 会话。
        private bool m_hasStarted; // 防止重复执行游戏入口。

        /// <summary>场景加载与首页打开共占两个真实 Loading 步骤。</summary>
        public int StepCount => 2;

        /// <summary>加载初始业务场景，等待首页就绪后关闭启动 Loading。</summary>
        /// <param name="loadingSession">框架交接且已经完成业务初始化的同一 Loading 会话。</param>
        /// <param name="cancellationToken">用于取消进入游戏流程的令牌。</param>
        /// <returns>首个业务界面就绪的任务。</returns>
        /// <exception cref="ArgumentNullException">共享的 Loading 会话为空。</exception>
        /// <exception cref="InvalidOperationException">入口重复执行或游戏首页尚未就绪。</exception>
        public async UniTask MainAsync(
            IStartupLoadingSession loadingSession,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (loadingSession == null)
            {
                throw new ArgumentNullException(nameof(loadingSession));
            }

            if (m_hasStarted)
            {
                throw new InvalidOperationException("游戏入口不能重复执行。");
            }

            m_hasStarted = true;
            m_loadingSession = loadingSession;

            // 业务入口集中在此，首页真正就绪后才结束启动展示。
            await LoadInitialSceneAsync(cancellationToken);
            await EnterInitialUIAsync(cancellationToken);
            CloseLoading();
        }

        /// <summary>解除 Loading 持有关系，并异步卸载游戏入口持有的场景。</summary>
        /// <returns>场景资源卸载完成的任务。</returns>
        public async UniTask ShutdownAsync()
        {
            // 启动失败时释放会话仍会保留独立的 AOT 错误展示。
            CloseLoading();
            var sceneHandle = m_sceneHandle;
            m_sceneHandle = null;

            if (sceneHandle != null && !sceneHandle.IsUnloaded)
            {
                EnsureSceneCanUnload(sceneHandle);
                await sceneHandle.UnloadAsync();
            }
        }

        private async UniTask LoadInitialSceneAsync(CancellationToken cancellationToken)
        {
            // 真实场景回调只更新本步骤，权重由共享会话统一累计。
            m_loadingSession?.BeginStep(EStartupLoadingStage.LoadingScene, "加载首个游戏场景");
            m_sceneHandle = await ResourceManager.Instance.LoadSceneAsync(
                "Scenes/SampleScene",
                OnSceneLoadProgress,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            m_loadingSession?.CompleteStep();
        }

        private async UniTask EnterInitialUIAsync(CancellationToken cancellationToken)
        {
            // 首页完成导航并获得一帧显示机会后才关闭 Loading，避免空白间隙。
            m_loadingSession?.BeginStep(EStartupLoadingStage.OpeningHome, "打开游戏首页");
            var homeHandle = await UIManager.Instance.JumpAsync(
                NavigationJumpID.HOME,
                cancellationToken: cancellationToken);
            await UniTask.NextFrame(cancellationToken);
            if (homeHandle == null || !homeHandle.IsValid || homeHandle.State != UIState.Active)
            {
                throw new InvalidOperationException("游戏首页尚未就绪，不能关闭启动 Loading。");
            }

            m_loadingSession?.CompleteStep();
        }

        private void CloseLoading()
        {
            var loadingSession = m_loadingSession;
            m_loadingSession = null;
            loadingSession?.Dispose();
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
            m_loadingSession?.Report(progress);
        }
    }
}
