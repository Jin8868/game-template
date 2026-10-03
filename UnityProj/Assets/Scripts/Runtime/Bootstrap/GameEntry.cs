using System.Threading;
using AlloyFramework;
using AlloyFramework.UI;
using Cysharp.Threading.Tasks;
using Game.UI;
using UnityEngine;
using UnityEngine.Scripting;

namespace Game
{
    [Preserve]
    public sealed class GameEntry : IGameEntry
    {
        private ISceneHandle m_sceneHandle;
        private UIHandle<LoadingUIController> m_loadingHandle;
        
        public async UniTask MainAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AlloyDebug.Log("Game entry started.");

            m_loadingHandle = await UIManager.Instance.OpenAsync(
                GameUI.LoadingUI, cancellationToken);
            await UniTask.NextFrame(cancellationToken);
            FrameworkBootstrap.DestroyStartupScreen();

            m_sceneHandle = await ResourceManager.Instance.LoadSceneAsync(
                "Scenes/SampleScene",
                LoadCallBack,
                cancellationToken: cancellationToken);
        }

        public async UniTask ShutdownAsync()
        {
            await CloseLoadingAsync();

            var sceneHandle = m_sceneHandle;
            m_sceneHandle = null;

            if (sceneHandle != null && !sceneHandle.IsUnloaded)
                await sceneHandle.UnloadAsync();
        }

        private async UniTask CloseLoadingAsync()
        {
            var loadingHandle = m_loadingHandle;
            m_loadingHandle = null;
            if (loadingHandle != null && loadingHandle.IsValid)
                await loadingHandle.CloseAsync();
        }

        private void LoadCallBack(float progress)
        {
            var loadingHandle = m_loadingHandle;
            if (loadingHandle == null || !loadingHandle.IsValid) return;

            var normalizedProgress = Mathf.Clamp01(progress);
            loadingHandle.Controller.RefreshProgress(normalizedProgress);

            if (normalizedProgress >= 1f)
                CloseLoadingAsync().Forget(exception => AlloyDebug.Error(exception));
        }
    }
}
