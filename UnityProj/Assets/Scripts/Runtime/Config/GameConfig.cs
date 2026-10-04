using System;
using System.Threading;
using AlloyFramework;
using Cysharp.Threading.Tasks;
using Game.Config;
using Luban;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 提供游戏启动阶段 Luban 配置的唯一加载入口。
    /// </summary>
    public static class GameConfig
    {
        private const string STARTUPCONFIGLOCATION = "Config/Luban/uijumpconfigtable";
        private const string UIJUMPTABLENAME = "uijumpconfigtable";

        private static IAssetHandle<TextAsset> m_startupConfigHandle; // 启动配置资源句柄。
        private static byte[] m_uiJumpConfigBytes; // UI 跳转配置的二进制数据。

        /// <summary>
        /// 获取已加载的启动配置表集合。
        /// </summary>
        public static Tables StartupTables { get; private set; }

        /// <summary>
        /// 获取启动配置是否已完成加载。
        /// </summary>
        public static bool IsStartupLoaded => StartupTables != null;

        /// <summary>
        /// 加载并构造启动阶段所需的全部配置表。
        /// </summary>
        /// <param name="cancellationToken">用于取消本次加载的令牌。</param>
        /// <returns>表示异步加载过程的任务。</returns>
        public static async UniTask LoadStartupAsync(CancellationToken cancellationToken)
        {
            if (StartupTables != null)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            IAssetHandle<TextAsset> configHandle = null;

            try
            {
                // 保持资源句柄，确保 Tables 使用的二进制数据在生命周期内有效。
                configHandle = await ResourceManager.Instance.LoadAssetAsync<TextAsset>(
                    STARTUPCONFIGLOCATION,
                    cancellationToken: cancellationToken);
                m_uiJumpConfigBytes = configHandle.Asset.bytes;
                StartupTables = new Tables(LoadTable);
                m_startupConfigHandle = configHandle;
            }
            catch
            {
                // 构造失败时释放本次取得的资源，保持未加载状态。
                configHandle?.Dispose();
                m_uiJumpConfigBytes = null;
                throw;
            }
        }

        /// <summary>
        /// 释放启动配置占用的资源。
        /// </summary>
        public static void UnloadStartup()
        {
            // 清理表实例和资源句柄，为后续重新初始化保留入口。
            StartupTables = null;
            m_uiJumpConfigBytes = null;
            m_startupConfigHandle?.Dispose();
            m_startupConfigHandle = null;
        }

        private static ByteBuf LoadTable(string tableName)
        {
            if (tableName != UIJUMPTABLENAME)
            {
                throw new InvalidOperationException($"未注册的启动配置表：{tableName}。");
            }

            if (m_uiJumpConfigBytes == null)
            {
                throw new InvalidOperationException("UI 跳转配置尚未加载。");
            }

            return ByteBuf.Wrap(m_uiJumpConfigBytes);
        }
    }
}
