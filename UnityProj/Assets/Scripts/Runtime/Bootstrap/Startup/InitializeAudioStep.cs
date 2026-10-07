using System.Threading;
using AlloyFramework.Audio;
using Cysharp.Threading.Tasks;

namespace Game
{
    internal sealed class InitializeAudioStep : IGameStartupStep
    {
        public string Name => "初始化框架音频与默认监听器";

        /// <summary>等待项目安装器交付 Init 并初始化引擎。</summary>
        /// <param name="cancellationToken">启动取消令牌。</param>
        /// <returns>引擎就绪任务。</returns>
        public UniTask ExecuteAsync(CancellationToken cancellationToken)
        { return AudioManager.Instance.ConfigureAndInitializeAsync(cancellationToken); }
    }
}
