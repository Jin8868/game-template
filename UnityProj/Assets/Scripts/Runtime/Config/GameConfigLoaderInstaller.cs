using AlloyFramework;
using UnityEngine;

namespace Game
{
    internal static class GameConfigLoaderInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            AlloyConfig.Instance.SetLoader(new GameConfigLoader());
        }
    }
}
