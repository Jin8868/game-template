using System;
using System.Threading;
using AlloyFramework;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Game
{
    /// <summary>统一协调业务模块初始化、进入游戏和逆序退出。</summary>
    [Preserve]
    public sealed class GameEntry : IGameEntry
    {
        private readonly GameStartupPipeline m_startupPipeline = new GameStartupPipeline(); // 可扩展的业务模块管线。

        private bool m_hasInitializationStarted; // 初始化失败后也不允许复用同一入口重试。
        private bool m_isInitialized; // 所有业务模块均已完成初始化。
        private bool m_hasStarted; // 防止重复执行游戏入口。

        /// <summary>通过可扩展业务管线安装项目模块，完成后才允许进入游戏。</summary>
        /// <param name="loadingSession">用于报告当前初始化操作的共享展示会话。</param>
        /// <param name="cancellationToken">用于取消业务初始化的令牌。</param>
        /// <returns>全部业务模块就绪的任务。</returns>
        /// <exception cref="ArgumentNullException">展示会话为空。</exception>
        /// <exception cref="InvalidOperationException">初始化重复执行。</exception>
        public async UniTask InitializeAsync(
            IStartupLoadingSession loadingSession,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (loadingSession == null) throw new ArgumentNullException(nameof(loadingSession));
            if (m_hasInitializationStarted) throw new InvalidOperationException("业务初始化不能重复执行。");

            // 入口只协调生命周期，新增模块仍在业务管线中通过 Add 注册。
            m_hasInitializationStarted = true;
            await m_startupPipeline.RunAsync(loadingSession, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            m_isInitialized = true;
        }

        /// <summary>在 Boot 场景完成空白模板启动，为后续业务入口保留接入位置。</summary>
        /// <param name="loadingSession">框架已开始游戏入口步骤的共享会话，仅用于报告内部进度。</param>
        /// <param name="cancellationToken">用于取消进入游戏流程的令牌。</param>
        /// <returns>模板启动完成的任务。</returns>
        /// <exception cref="ArgumentNullException">共享的 Loading 会话为空。</exception>
        /// <exception cref="InvalidOperationException">业务尚未初始化或入口重复执行。</exception>
        public async UniTask MainAsync(
            IStartupLoadingSession loadingSession,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (loadingSession == null)
            {
                throw new ArgumentNullException(nameof(loadingSession));
            }

            if (!m_isInitialized || m_hasStarted)
            {
                throw new InvalidOperationException("游戏入口需要业务初始化完成，且不能重复执行。");
            }

            // 空白模板不加载演示场景或页面，后续项目在此接入业务入口。
            m_hasStarted = true;
            await UniTask.NextFrame(cancellationToken);
            loadingSession.Report(1f);
        }

        /// <summary>逆序退出已开始的业务模块。</summary>
        /// <returns>业务资源退出清理完成的任务。</returns>
        public async UniTask ShutdownAsync()
        {
            // 初始化中途失败时也退出已启动模块，避免残留网络和模型状态。
            m_isInitialized = false;
            await m_startupPipeline.ShutdownAsync();
        }
    }
}