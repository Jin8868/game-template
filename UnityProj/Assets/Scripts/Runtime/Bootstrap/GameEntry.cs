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
    [Preserve]
    public sealed class GameEntry : IGameEntry
    {
        private const string SHUTDOWNSCENENAME = "AlloyFrameworkShutdown"; // 卸载最后一个场景时的承接场景。
        private const int INITIALGAMESTEPCOUNT = 2; // 首场景加载与首页打开两个真实步骤。

        private readonly GameStartupPipeline m_startupPipeline = new GameStartupPipeline(); // 业务启动步骤管线。
        private ISceneHandle m_sceneHandle; // 当前加载的游戏场景句柄。
        private IStartupLoadingSession m_loadingSession; // 框架交付的同一 AOT Loading 控制权。
        private bool m_hasStarted; // 防止重复接管和重复执行业务入口。

        /// <summary>
        /// 执行业务启动流程并进入首个游戏场景。
        /// </summary>
        /// <param name="cancellationToken">用于取消本次启动流程的令牌。</param>
        /// <returns>表示异步启动流程的任务。</returns>
        /// <exception cref="InvalidOperationException">重复执行业务入口或框架尚未交出 Loading。</exception>
        public async UniTask MainAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (m_hasStarted) throw new InvalidOperationException("业务入口不能重复执行。");
            m_hasStarted = true;

            // 接管早于异步业务初始化，显式从框架百分之百重置为业务零进度。
            m_loadingSession = StartupLoading.TakeOver(m_startupPipeline.StepCount + INITIALGAMESTEPCOUNT);
            AlloyDebug.Log("[GameStartup] 业务已接管统一 Loading。");
            try
            {
                // 模型注册、框架扩展、网络和音频安装都是必需步骤，不能省略或重复执行。
                await m_startupPipeline.RunAsync(m_loadingSession, cancellationToken);
                await LoadInitialSceneAsync(cancellationToken);
                await EnterInitialUIAsync(cancellationToken);
                CloseLoading();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // 显示并冻结具体失败步骤，再由框架逆序清理，保留独立的 AOT 错误面板。
                m_loadingSession?.Fail(exception);
                throw;
            }
        }

        /// <summary>
        /// 关闭业务运行期间持有的场景、界面和配置资源。
        /// </summary>
        /// <returns>表示异步关闭流程的任务。</returns>
        public async UniTask ShutdownAsync()
        {
            // 解除业务展示持有关系；启动失败时 AOT 错误面板仍然保留。
            CloseLoading();

            // 管线逆序释放网络等项目接入，再关闭原生音频和框架资源。
            m_startupPipeline.Shutdown();
            await AudioManager.Instance.ShutdownAsync();

            // 释放首个业务场景的资源句柄。
            var sceneHandle = m_sceneHandle;
            m_sceneHandle = null;

            if (sceneHandle != null && !sceneHandle.IsUnloaded)
            {
                EnsureSceneCanUnload(sceneHandle);
                await sceneHandle.UnloadAsync();
            }
        }

        private void CloseLoading()
        {
            var session = m_loadingSession;
            m_loadingSession = null;
            session?.Dispose();
        }

        private async UniTask LoadInitialSceneAsync(CancellationToken cancellationToken)
        {
            // 真实场景回调只更新本步骤，权重由同一个聚合器计算。
            m_loadingSession.BeginStep(EStartupLoadingStage.LoadingScene, "加载首个游戏场景");
            m_sceneHandle = await ResourceManager.Instance.LoadSceneAsync(
                "Scenes/SampleScene",
                OnSceneLoadProgress,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            m_loadingSession.CompleteStep();
        }

        private async UniTask EnterInitialUIAsync(CancellationToken cancellationToken)
        {
            // 首页完成导航并获得一帧显示机会后才关闭 Loading，避免空白间隙。
            m_loadingSession.BeginStep(EStartupLoadingStage.OpeningHome, "打开游戏首页");
            var homeHandle = await UIManager.Instance.JumpAsync(
                NavigationJumpID.HOME,
                cancellationToken: cancellationToken);
            await UniTask.NextFrame(cancellationToken);
            if (homeHandle == null || !homeHandle.IsValid || homeHandle.State != UIState.Active)
            {
                throw new InvalidOperationException("游戏首页尚未就绪，不能关闭启动 Loading。");
            }
            m_loadingSession.CompleteStep();
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
