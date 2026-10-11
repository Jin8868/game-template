using UnityEngine;

namespace Game
{
    internal static class GameLogViewerInstaller
    {
        private const string PREFABLOCATION = "GameLogViewer";

        internal static void Install()
        {
            // 使用主包内置预制体，资源包初始化失败时仍能查看启动日志。
            GameObject prefab = Resources.Load<GameObject>(PREFABLOCATION);
            if (prefab == null)
            {
                Debug.LogError("缺少 Resources/GameLogViewer 日志预制体。");
                return;
            }
            Object.Instantiate(prefab);
        }
    }
}
