using System.Threading;
using AlloyFramework;
using AlloyFramework.Audio.Wwise;
using Cysharp.Threading.Tasks;

namespace Game
{
    internal static class GameAudioInstaller
    {
        internal static UniTask InstallAsync(CancellationToken cancellationToken)
        {
            // 项目只配置资源约定，平台识别、内容加载及后端安装由框架完成。
            return WwiseAudioInstaller.InstallAsync(
                "AudioPackage",
                "WwiseAudio",
                "Config/WwiseInitializationSettings",
                ResourceSettings.DefaultPackageName,
                cancellationToken);
        }
    }
}