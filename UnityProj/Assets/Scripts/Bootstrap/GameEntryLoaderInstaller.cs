using AlloyFramework;
using AlloyFramework.Audio;
using UnityEngine;

namespace Game
{
    internal static class GameEntryLoaderInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            FrameworkBootstrap.SetGameEntryLoader(new DefaultGameEntryLoader());
            AudioManager.Instance.SetInstaller(GameAudioInstaller.InstallAsync);
        }
    }
}
