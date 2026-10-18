using System.Threading;
using AlloyFramework;
using Cysharp.Threading.Tasks;

namespace Game
{
    internal sealed class RegisterGameModelsStep : IGameStartupStep
    {
        /// <summary>当前步骤的日志名称。</summary>
        public string Name => "注册业务 Model";

        /// <summary>当前步骤占用一个真实 Loading 步骤。</summary>
        public int StepCount => 1;

        /// <summary>调用集中注册入口，尚不创建具体 Model 实例。</summary>
        /// <param name="loadingSession">共享的 Loading 会话；无展示时可为空。</param>
        /// <param name="cancellationToken">用于取消启动的令牌。</param>
        /// <returns>已完成的注册任务。</returns>
        public UniTask ExecuteAsync(
            IStartupLoadingSession loadingSession,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 由当前业务步骤报告实际执行阶段。
            loadingSession?.BeginStep(EStartupLoadingStage.InitializingBusiness, Name);
            GameModelRegistration.RegisterModels();
            cancellationToken.ThrowIfCancellationRequested();
            loadingSession?.CompleteStep();
            return UniTask.CompletedTask;
        }
    }
}
