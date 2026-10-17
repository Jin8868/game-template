using UnityEngine;

namespace Game
{
    /// <summary>
    /// 在业务 UI 初始化前提供独立的游戏内日志绘制。
    /// </summary>
    [DefaultExecutionOrder(-12000)]
    [RequireComponent(typeof(Reporter))]
    public sealed class GameLogViewerController : MonoBehaviour
    {
        private static GameLogViewerController m_instance; // 独立于业务资源系统的日志窗口实例。
        private Reporter m_reporter; // 当前对象上的第三方日志收集器。

        /// <summary>日志窗口是否正在显示，供启动画布让出显示和输入。</summary>
        public static bool IsVisible => m_instance != null &&
            m_instance.m_reporter != null && m_instance.m_reporter.show;

        private void Awake()
        {
            m_instance = this;
            m_reporter = GetComponent<Reporter>();
        }

        private void OnDestroy()
        {
            if (m_instance == this) m_instance = null;
        }

        private void OnGUI()
        {
            // GUI.depth 仅控制 IMGUI 的绘制顺序，启动 Canvas 由展示器主动让出。
            int previousDepth = GUI.depth;
            GUI.depth = -10000;
            try
            {
                m_reporter.OnGUIDraw();
            }
            finally
            {
                GUI.depth = previousDepth;
            }
        }
    }
}
