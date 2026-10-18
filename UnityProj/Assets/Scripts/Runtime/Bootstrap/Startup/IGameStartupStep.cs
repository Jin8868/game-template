using System.Threading;
using AlloyFramework;
using Cysharp.Threading.Tasks;

namespace Game
{
    internal interface IGameStartupStep
    {
        /// <summary>启动步骤的日志名称。</summary>
        string Name { get; }

        /// <summary>当前步骤包含的真实 Loading 步骤数。</summary>
        int StepCount { get; }

        /// <summary>
        /// 执行当前业务启动步骤。
        /// </summary>
        /// <param name="loadingSession">共享的 Loading 会话；无展示时可为空，由步骤报告进度。</param>
        /// <param name="cancellationToken">用于取消启动流程的令牌。</param>
        /// <returns>表示步骤执行过程的任务。</returns>
        UniTask ExecuteAsync(IStartupLoadingSession loadingSession, CancellationToken cancellationToken);
    }
}
