using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game
{
    internal interface IGameStartupStep
    {
        /// <summary>启动步骤的日志名称。</summary>
        string Name { get; }

        /// <summary>
        /// 执行当前业务启动步骤。
        /// </summary>
        /// <param name="cancellationToken">用于取消启动流程的令牌。</param>
        /// <returns>表示步骤执行过程的任务。</returns>
        UniTask ExecuteAsync(CancellationToken cancellationToken);
    }
}
