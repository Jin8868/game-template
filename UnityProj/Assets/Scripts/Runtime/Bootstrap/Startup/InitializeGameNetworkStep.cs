using System;
using System.Threading;
using AlloyFramework;
using Cysharp.Threading.Tasks;
using Game.Network;

namespace Game
{
    /// <summary>安装项目网络服务注册管理，并持有运行时生命周期。</summary>
    internal sealed class InitializeGameNetworkStep : IGameStartupStep, IDisposable
    {
        private GameNetworkRuntime m_networkRuntime; // 当前启动步骤持有的网络运行时。

        /// <summary>当前启动步骤的日志名称。</summary>
        public string Name => "初始化项目网络";

        /// <summary>当前步骤占用一个真实 Loading 步骤。</summary>
        public int StepCount => 1;

        /// <summary>安装项目网络注册表，具体协议与连接由项目接入。</summary>
        /// <param name="loadingSession">共享的 Loading 会话；无展示时可为空。</param>
        /// <param name="cancellationToken">启动取消令牌。</param>
        /// <returns>网络安装完成任务。</returns>
        public UniTask ExecuteAsync(
            IStartupLoadingSession loadingSession,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 由当前业务步骤报告实际执行阶段。
            loadingSession?.BeginStep(EStartupLoadingStage.InitializingBusiness, Name);

            // 注册管理在业务入口执行前安装，退出时由管线统一释放。
            m_networkRuntime = new GameNetworkRuntime();
            m_networkRuntime.Initialize();
            cancellationToken.ThrowIfCancellationRequested();
            loadingSession?.CompleteStep();
            return UniTask.CompletedTask;
        }

        /// <summary>解除网络注册管理并释放项目服务。</summary>
        public void Dispose()
        {
            m_networkRuntime?.Dispose();
            m_networkRuntime = null;
        }
    }
}
