using System.Threading;
using AlloyFramework;
using AlloyFramework.Audio;
using Cysharp.Threading.Tasks;

namespace Game
{
    internal sealed class InitializeAudioStep : IGameStartupStep
    {
        /// <summary>当前启动步骤的日志名称。</summary>
        public string Name => "初始化框架音频与默认监听器";

        /// <summary>当前步骤占用一个真实 Loading 步骤。</summary>
        public int StepCount => 1;

        /// <summary>等待项目安装器交付 Init 并初始化引擎。</summary>
        /// <param name="loadingSession">共享的 Loading 会话；无展示时可为空。</param>
        /// <param name="cancellationToken">启动取消令牌。</param>
        /// <returns>引擎就绪任务。</returns>
        public async UniTask ExecuteAsync(
            IStartupLoadingSession loadingSession,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 等待音频真正就绪后才累计启动进度。
            loadingSession?.BeginStep(EStartupLoadingStage.InitializingBusiness, Name);
            await AudioManager.Instance.ConfigureAndInitializeAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            loadingSession?.CompleteStep();
        }
    }
}
