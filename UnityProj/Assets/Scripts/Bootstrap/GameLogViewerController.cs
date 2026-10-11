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
        private Reporter m_reporter; // 当前对象上的第三方日志收集器。

        private void Awake()
        {
            m_reporter = GetComponent<Reporter>();
        }

        private void OnGUI()
        {
            // 保持日志面板在启动画面上方，不依赖业务 Canvas 或资源包。
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
