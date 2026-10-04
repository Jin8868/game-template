using System.Threading;
using AlloyFramework;
using AlloyFramework.UI;
using Cysharp.Threading.Tasks;
using Game.Config;
using Game.UI;
using UnityEngine;
using UnityEngine.Scripting;

namespace Game
{
    [Preserve]
    public sealed class GameEntry : IGameEntry
    {
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

            // 打开启动加载界面后再开始场景加载，避免启动过程缺少反馈。
            m_loadingHandle = await UIManager.Instance.OpenAsync(
                GameUI.LoadingUI, cancellationToken);
            await UniTask.NextFrame(cancellationToken);
            FrameworkBootstrap.DestroyStartupScreen();
            var configData = AlloyConfig.Instance.Get<UIJumpConfig>(10001);
            AlloyDebug.Log(configData.BackJumpId);
            // 加载首个业务场景并将进度反馈给启动界面。
            m_sceneHandle = await ResourceManager.Instance.LoadSceneAsync(
                "Scenes/SampleScene",
                LoadCallBack,
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

        private void LoadCallBack(float progress)
        {
            var loadingHandle = m_loadingHandle;
            if (loadingHandle == null || !loadingHandle.IsValid)
            {
                return;
            }

            var normalizedProgress = Mathf.Clamp01(progress);
            loadingHandle.Controller.RefreshProgress(normalizedProgress);

            if (normalizedProgress >= 1f)
            {
                CloseLoadingAsync().Forget(LogCloseLoadingException);
            }
        }

        private static void LogCloseLoadingException(System.Exception exception)
        {
            AlloyDebug.Error(exception);
        }
    }
}
