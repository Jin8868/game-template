using System.Threading;
using AlloyFramework;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game
{
    public sealed class GameEntry : IGameEntry
    {
        public async UniTask MainAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AlloyDebug.Log("Game entry started.");
            
            await ResourceManager.Instance.LoadSceneAsync(
                "Scenes/SampleScene",
                cancellationToken: cancellationToken);
        }

        public void Shutdown()
        {
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
