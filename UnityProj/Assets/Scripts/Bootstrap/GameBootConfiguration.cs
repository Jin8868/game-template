using System;
using AlloyFramework;
using UnityEngine;

namespace Game
{
    /// <summary>Boot 场景中的共享启动配置入口和更新状态显示。</summary>
    public sealed class GameBootConfiguration : MonoBehaviour
    {
        [SerializeField]
        private GameHotUpdateSettings m_settings; // 与 Resources 启动和编辑器构建共用的配置资产。
        private static string m_status; // 场景载入前也可以记录启动状态。
        private static bool m_failed; // 启动失败时保留提示，等待用户重启。

        public GameHotUpdateSettings Settings => m_settings;

        internal static void InstallStatus()
        {
            m_status = "正在启动……";
            m_failed = false;
            FrameworkBootstrap.StartupFailed -= OnStartupFailed;
            FrameworkBootstrap.StartupFailed += OnStartupFailed;
            ResourceSettings.PackageDownloadProgress = OnDownloadProgress;
        }

        internal static void SetStatus(string status)
        {
            m_status = status;
        }

        private static void OnStartupFailed(Exception exception)
        {
            m_failed = true;
            m_status = "启动失败，请检查服务器或查看日志后重启。\n" + exception.GetBaseException().Message;
        }

        private static void OnDownloadProgress(string packageName, float fraction, long currentBytes, long totalBytes)
        {
            m_status = $"更新 {packageName}：{fraction:P0}\n" +
                $"{currentBytes / 1048576f:F2} / {totalBytes / 1048576f:F2} MB";
        }

        private void OnGUI()
        {
            if (!m_failed && FrameworkBootstrap.IsInitialized)
            {
                return;
            }
            // 独立于业务 UI 的提示，业务资源加载失败时仍可显示原因。
            int previousDepth = GUI.depth;
            GUI.depth = -9000;
            try
            {
                GUILayout.BeginArea(new Rect(20, 20, Mathf.Min(Screen.width - 40, 800), 180), GUI.skin.box);
                GUILayout.Label(m_status);
                GUILayout.EndArea();
            }
            finally
            {
                GUI.depth = previousDepth;
            }
        }
    }
}
