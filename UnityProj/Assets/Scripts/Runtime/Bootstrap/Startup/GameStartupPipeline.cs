using System;
using System.Collections.Generic;
using System.Threading;
using AlloyFramework;
using Cysharp.Threading.Tasks;

namespace Game
{
    internal sealed class GameStartupPipeline
    {
        private readonly List<IGameStartupStep> m_steps = new List<IGameStartupStep>(); // 按声明顺序执行。
        private int m_startedStepCount; // 已开始执行、需要逆序释放的步骤数。
        private bool m_hasRun; // 管线是否已经执行过。

        internal int StepCount => m_steps.Count;

        internal GameStartupPipeline()
        {
            // 显式声明项目安装顺序，网络注册管理在进入首个页面前安装。
            Add(new RegisterGameModelsStep());
            Add(new ConfigureFrameworkExtensionsStep());
            Add(new InitializeGameNetworkStep());
            Add(new InitializeAudioStep());
        }

        internal GameStartupPipeline(params IGameStartupStep[] steps)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            foreach (var step in steps)
            {
                Add(step);
            }
        }

        /// <summary>按调用顺序添加项目启动步骤。</summary>
        /// <param name="step">需要执行的步骤。</param>
        internal void Add(IGameStartupStep step)
        {
            // 执行开始后冻结步骤列表，避免运行与退出顺序发生变化。
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            if (m_hasRun)
            {
                throw new InvalidOperationException("业务启动管线执行后不能添加步骤。");
            }

            m_steps.Add(step);
        }

        /// <summary>执行全部业务启动步骤。</summary>
        /// <param name="cancellationToken">用于取消启动流程的令牌。</param>
        /// <returns>完整启动过程的任务。</returns>
        internal UniTask RunAsync(CancellationToken cancellationToken)
        {
            return RunAsync(null, cancellationToken);
        }

        /// <summary>在统一 Loading 下执行所有必需业务步骤。</summary>
        /// <param name="loadingSession">本轮业务会话；独立使用管线时可为空。</param>
        /// <param name="cancellationToken">用于取消启动流程的令牌。</param>
        /// <returns>全部业务安装完成任务。</returns>
        internal async UniTask RunAsync(
            IStartupLoadingSession loadingSession,
            CancellationToken cancellationToken)
        {
            if (m_hasRun)
            {
                throw new InvalidOperationException("业务启动管线不能重复执行。");
            }

            // 执行前记录当前步骤，失败步骤持有的资源也参与退出清理。
            m_hasRun = true;
            for (int index = 0; index < m_steps.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var step = m_steps[index];
                m_startedStepCount = index + 1;
                var startedAt = DateTime.UtcNow;
                loadingSession?.BeginStep(EStartupLoadingStage.InitializingBusiness, step.Name);
                AlloyDebug.Log($"[GameStartup] 开始：{step.Name}");
                try
                {
                    await step.ExecuteAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    loadingSession?.CompleteStep();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    AlloyDebug.Log($"[GameStartup] 取消：{step.Name}");
                    throw;
                }
                catch (Exception exception)
                {
                    var failedElapsed = DateTime.UtcNow - startedAt;
                    AlloyDebug.Error($"[GameStartup] {step.Name} 失败，耗时 {failedElapsed.TotalMilliseconds:F1} ms");
                    loadingSession?.Fail(exception);
                    throw;
                }
                var elapsed = DateTime.UtcNow - startedAt;
                AlloyDebug.Log($"[GameStartup] 完成：{step.Name}，耗时 {elapsed.TotalMilliseconds:F1} ms");
            }
        }

        internal void Shutdown()
        {
            // 只逆序释放已经开始的步骤，单个失败不阻止其他资源清理。
            while (m_startedStepCount > 0)
            {
                m_startedStepCount--;
                if (m_steps[m_startedStepCount] is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception exception)
                    {
                        AlloyDebug.Error(exception);
                    }
                }
            }
        }
    }
}
