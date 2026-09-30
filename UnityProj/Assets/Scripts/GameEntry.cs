using System.Threading;
using AlloyFramework;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game
{
    public sealed class GameEntry : IGameEntry
    {
        public UniTask MainAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AlloyDebug.Log("Game entry started.");
            return UniTask.CompletedTask;
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
