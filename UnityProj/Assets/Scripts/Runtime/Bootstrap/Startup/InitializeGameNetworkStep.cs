using System;
using System.Threading;
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

        /// <summary>安装项目网络注册表，具体协议与连接由项目接入。</summary>
        /// <param name="cancellationToken">启动取消令牌。</param>
        /// <returns>网络安装完成任务。</returns>
        public UniTask ExecuteAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 注册管理在业务入口执行前安装，退出时由管线统一释放。
            m_networkRuntime = new GameNetworkRuntime();
            m_networkRuntime.Initialize();
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
