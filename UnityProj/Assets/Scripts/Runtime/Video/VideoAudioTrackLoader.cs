using System.Threading;
using AlloyFramework.Audio;
using Cysharp.Threading.Tasks;
using PulseVideoPlayer;

namespace Game.Video
{
    /// <summary>视频长音轨的独立准备入口，使用同一套 Wwise 会话、音轨作用域与同步逻辑。</summary>
    internal static class VideoAudioTrackLoader
    {
        internal static async UniTask PreloadAsync(
            string eventKey, AudioScope scope, PulsePreparationTimings timings,
            CancellationToken cancellationToken)
        {
            // 文件包资格由视频事件白名单和构建清单确定，此处不改变普通音效的全局设置。
            using (var trace = new VideoAudioPreparationTrace(eventKey, timings))
            {
                await AudioManager.Instance.PreloadAudioAsync(eventKey, scope, cancellationToken);
            }
        }
    }
}
