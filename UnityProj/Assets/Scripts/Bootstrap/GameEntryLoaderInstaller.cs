using AlloyFramework;
using UnityEngine;

namespace Game
{
    internal static class GameEntryLoaderInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            FrameworkBootstrap.SetGameEntryLoader(new DefaultGameEntryLoader());
        }
    }
}