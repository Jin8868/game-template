using AlloyFramework;
using AlloyFramework.Audio;
using AlloyFramework.HybridCLR;
using UnityEngine;

namespace Game
{
    internal static class GameEntryLoaderInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
#if UNITY_EDITOR
            // 独立测试场景跳过业务安装，避免日志面板、热更和音频接管测试环境。
            if (UnityEditor.SessionState.GetBool("AlloyFramework.SkipAutomaticStartup", false)) return;
#endif
            // 最先安装 AOT 日志面板，记录后续入口与资源初始化的错误。
            GameLogViewerInstaller.Install();

            // 代码准备必须早于业务配置初始化；业务入口只消费准备好的程序集。
            GameHotUpdateSettings settings = GameHotUpdateSettings.Load();
            GameRemoteRelease.Configure(settings);
            if (settings.CodeMode == EGameCodeMode.HybridCLR)
            {
                var result = new GameCodePreparationResult();
                FrameworkBootstrap.SetGameCodePreparation(new HybridCLRCodePreparation(
                    result, settings.RuntimeAssemblyName, settings.InstallerTypeName));
                FrameworkBootstrap.SetGameEntryLoader(new HybridCLRGameEntryLoader(result));
            }
            else
            {
                FrameworkBootstrap.SetGameCodePreparation(new DefaultGameCodePreparation(settings));
                FrameworkBootstrap.SetGameEntryLoader(new DefaultGameEntryLoader());
            }
            AudioManager.Instance.SetInstaller(GameAudioInstaller.InstallAsync);
        }
    }
}
