using System.Threading;
using AlloyFramework;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game
{
    public sealed class GameEntry : IGameEntry
    {
        private ISceneHandle m_sceneHandle;
        
        public async UniTask MainAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AlloyDebug.Log("Game entry started.");
            
            m_sceneHandle = await ResourceManager.Instance.LoadSceneAsync(
                "Scenes/SampleScene",
                cancellationToken: cancellationToken);
        }

        public async UniTask ShutdownAsync()
        {
            var sceneHandle = m_sceneHandle;
            m_sceneHandle = null;

            if (sceneHandle != null && !sceneHandle.IsUnloaded)
                await sceneHandle.UnloadAsync();
        }
    }

    internal static class GameEntryInjector
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Inject()
        {
            FrameworkBootstrap.SetGameEntry(new GameEntry());
        }
    }
}
