using UnityEngine;

namespace Game
{
    internal static class GameLogViewerInstaller
    {
        private const string PREFABLOCATION = "GameLogViewer"; // 主包内置日志预制体位置。
        private static GameObject m_instanceRoot; // 避免 Awake 尚未执行时重复创建日志节点。

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallBeforeSceneLoad()
        {
            // 等待引擎子系统准备完成后再创建 MonoBehaviour 和输入界面。
            Install();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CheckInstallation()
        {
            Install();
            Reporter reporter = m_instanceRoot != null ? m_instanceRoot.GetComponent<Reporter>() : null;
            Debug.Log($"[GameLogViewer] 安装检查：控制器={GameLogViewerController.IsInstalled}，" +
                $"对象激活={m_instanceRoot != null && m_instanceRoot.activeInHierarchy}，" +
                $"收集器就绪={reporter != null && reporter.Initialized}。");
        }

        internal static void Install()
        {
            if (GameLogViewerController.IsInstalled || m_instanceRoot != null) return;
            // 使用主包内置预制体，资源包初始化失败时仍能查看启动日志。
            GameObject prefab = Resources.Load<GameObject>(PREFABLOCATION);
            if (prefab == null)
            {
                Debug.LogError("缺少 Resources/GameLogViewer 日志预制体。");
                return;
            }
            m_instanceRoot = Object.Instantiate(prefab);
            m_instanceRoot.SetActive(true);
        }
    }
}
