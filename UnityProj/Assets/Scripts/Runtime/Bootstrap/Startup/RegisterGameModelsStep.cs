using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game
{
    internal sealed class RegisterGameModelsStep : IGameStartupStep
    {
        /// <summary>当前步骤的日志名称。</summary>
        public string Name => "注册业务 Model";

        /// <summary>调用集中注册入口，尚不创建具体 Model 实例。</summary>
        /// <param name="cancellationToken">用于取消启动的令牌。</param>
        /// <returns>已完成的注册任务。</returns>
        public UniTask ExecuteAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GameModelRegistration.RegisterModels();
            return UniTask.CompletedTask;
        }
    }
}
