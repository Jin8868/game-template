using System;
using System.Threading;
using AlloyFramework;
using Cysharp.Threading.Tasks;

namespace Game
{
    internal sealed class GameStartupPipeline
    {
        private readonly IGameStartupStep[] m_steps; // 按业务声明顺序执行的启动步骤。
        private bool m_hasRun; // 管线是否已经执行过。

        /// <summary>
        /// 创建按声明顺序执行的业务启动管线。
        /// </summary>
        /// <param name="steps">需要依次执行的启动步骤。</param>
        internal GameStartupPipeline(params IGameStartupStep[] steps)
        {
            m_steps = steps ?? throw new ArgumentNullException(nameof(steps));
        }

        /// <summary>
        /// 执行全部业务启动步骤。
        /// </summary>
        /// <param name="cancellationToken">用于取消启动流程的令牌。</param>
        /// <returns>表示完整启动过程的任务。</returns>
        internal async UniTask RunAsync(CancellationToken cancellationToken)
        {
            if (m_hasRun)
            {
                throw new InvalidOperationException("业务启动管线不能重复执行。");
            }

            m_hasRun = true;
            for (var index = 0; index < m_steps.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var step = m_steps[index] ?? throw new InvalidOperationException(
                    $"业务启动管线的第 {index} 个步骤为空。");
                var startedAt = DateTime.UtcNow;
                AlloyDebug.Log($"[GameStartup] 开始：{step.Name}");
                await step.ExecuteAsync(cancellationToken);
                var elapsed = DateTime.UtcNow - startedAt;
                AlloyDebug.Log($"[GameStartup] 完成：{step.Name}，耗时 {elapsed.TotalMilliseconds:F1} ms");
            }
        }
    }
}
