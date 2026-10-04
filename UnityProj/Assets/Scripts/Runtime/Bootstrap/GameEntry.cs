using System.Threading;
using AlloyFramework;
using AlloyFramework.UI;
using Cysharp.Threading.Tasks;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace Game
{
    [Preserve]
    public sealed class GameEntry : IGameEntry
    {
        private const string SHUTDOWNSCENENAME = "AlloyFrameworkShutdown";

        private ISceneHandle m_sceneHandle; // 当前加载的游戏场景句柄。
        private UIHandle<LoadingUIController> m_loadingHandle; // 启动加载界面的运行时句柄。

        /// <summary>
        /// 执行业务启动流程并进入首个游戏场景。
        /// </summary>
        /// <param name="cancellationToken">用于取消本次启动流程的令牌。</param>
        /// <returns>表示异步启动流程的任务。</returns>
        public async UniTask MainAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AlloyDebug.Log("Game entry started.");

            // 业务启动步骤统一完成框架扩展安装，入口不直接依赖具体配置适配器。
            var startupPipeline = new GameStartupPipeline(
                new ConfigureFrameworkExtensionsStep());
            await startupPipeline.RunAsync(cancellationToken);

            // 打开启动加载界面后再开始场景加载，避免启动过程缺少反馈。
            m_loadingHandle = await UIManager.Instance.OpenAsync(
                GameUI.LoadingUI, cancellationToken);
            await UniTask.NextFrame(cancellationToken);
            FrameworkBootstrap.DestroyStartupScreen();

            // 加载首个业务场景并将进度反馈给启动界面。
            m_sceneHandle = await ResourceManager.Instance.LoadSceneAsync(
                "Scenes/SampleScene",
                LoadCallBack,
                cancellationToken: cancellationToken);

            // 场景加载完成后关闭启动界面，并通过配置 ID 进入导航验证首页。
            await CloseLoadingAsync();
            await UIManager.Instance.JumpAsync(
                NavigationJumpID.HOME,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 关闭业务运行期间持有的场景、界面和配置资源。
        /// </summary>
        /// <returns>表示异步关闭流程的任务。</returns>
        public async UniTask ShutdownAsync()
        {
            // 先关闭启动界面，避免卸载场景后残留 UI。
            await CloseLoadingAsync();

            // 释放首个业务场景的资源句柄。
            var sceneHandle = m_sceneHandle;
            m_sceneHandle = null;

            if (sceneHandle != null && !sceneHandle.IsUnloaded)
            {
                EnsureSceneCanUnload(sceneHandle);
                await sceneHandle.UnloadAsync();
            }

        }

        private async UniTask CloseLoadingAsync()
        {
            var loadingHandle = m_loadingHandle;
            m_loadingHandle = null;
            if (loadingHandle != null && loadingHandle.IsValid)
            {
                await loadingHandle.CloseAsync();
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

        private void LoadCallBack(float progress)
        {
            var loadingHandle = m_loadingHandle;
            if (loadingHandle == null || !loadingHandle.IsValid)
            {
                return;
            }

            var normalizedProgress = Mathf.Clamp01(progress);
            loadingHandle.Controller.RefreshProgress(normalizedProgress);
        }
    }
}
