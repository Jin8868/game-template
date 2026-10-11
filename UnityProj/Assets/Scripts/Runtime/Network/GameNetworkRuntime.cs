using System;
using AlloyFramework.Net;

namespace Game.Network
{
    /// <summary>持有项目网络服务注册表，统一安装和释放。</summary>
    internal sealed class GameNetworkRuntime : IDisposable
    {
        private readonly NetworkServiceRegistry m_services = new NetworkServiceRegistry(); // 项目的服务实例集合。
        private bool m_installed; // 注册表是否已经安装到网络管理器。

        internal void Initialize()
        {
            // 此处仅安装通用注册管理，实际会话与协议由项目接入时配置。
            NetworkManager.Instance.SetServiceRegistry(m_services);
            m_installed = true;
        }

        /// <summary>解除项目服务注册表并释放已注册服务。</summary>
        public void Dispose()
        {
            if (!m_installed)
            {
                return;
            }

            // 解除注册管理，由管理器统一释放已注册服务。
            NetworkManager.Instance.RemoveServiceRegistry(m_services);
            m_installed = false;
        }
    }
}
