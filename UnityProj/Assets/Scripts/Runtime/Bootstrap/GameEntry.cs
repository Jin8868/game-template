using System;
using System.Threading;
using AlloyFramework;
using AlloyFramework.Audio;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Game
{
    [Preserve]
    public sealed class GameEntry : IGameEntry
    {
        private readonly GameStartupPipeline m_startupPipeline = new GameStartupPipeline(); // 业务启动步骤管线。
        private IStartupLoadingSession m_loadingSession; // 框架交付的同一 AOT Loading 控制权。
        private bool m_hasStarted; // 防止重复接管和重复执行业务入口。

        /// <summary>
        /// 接管启动 Loading，并执行管线中注册的全部业务启动步骤。
        /// </summary>
        /// <param name="cancellationToken">用于取消本次启动流程的令牌。</param>
        /// <returns>表示异步启动流程的任务。</returns>
        /// <exception cref="InvalidOperationException">重复执行业务入口或框架尚未交出 Loading。</exception>
        public async UniTask MainAsync(CancellationToken cancellationToken)
        {
            // 防止重复启动，接管后由业务步骤更新同一份 Loading。
            cancellationToken.ThrowIfCancellationRequested();
            if (m_hasStarted)
            {
                throw new InvalidOperationException("业务入口不能重复执行。");
            }

            m_hasStarted = true;
            m_loadingSession = StartupLoading.TakeOver(m_startupPipeline.StepCount);
            AlloyDebug.Log("[GameStartup] 业务已接管统一 Loading。");
            try
            {
                // 初始化与进入游戏均由管线中显式注册的业务步骤负责。
                await m_startupPipeline.RunAsync(m_loadingSession, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // 冻结具体失败步骤，再由框架触发逆序清理并保留 AOT 错误面板。
                m_loadingSession?.Fail(exception);
                throw;
            }
        }

        /// <summary>
        /// 逆序释放业务步骤持有的资源，并关闭框架音频。
        /// </summary>
        /// <returns>表示异步关闭流程的任务。</returns>
        public async UniTask ShutdownAsync()
        {
            // 兜底解除 Loading 持有关系；启动失败时 AOT 错误面板仍然保留。
            var loadingSession = m_loadingSession;
            m_loadingSession = null;
            loadingSession?.Dispose();

            // 等待业务场景卸载，再释放其他项目接入和框架音频。
            await m_startupPipeline.ShutdownAsync();
            await AudioManager.Instance.ShutdownAsync();
        }
    }
}
