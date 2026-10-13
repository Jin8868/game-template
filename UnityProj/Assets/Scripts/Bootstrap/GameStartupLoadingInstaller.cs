using System;
using UnityEngine;

namespace Game
{
    internal static class GameStartupLoadingInstaller
    {
        internal static void EnsurePresenter()
        {
            if (UnityEngine.Object.FindObjectOfType<GameStartupLoadingPresenter>() != null) return;
            GameObject prefab = Resources.Load<GameObject>("GameStartupLoading");
            if (prefab == null)
                throw new InvalidOperationException("主包缺少 Resources/GameStartupLoading 内置启动界面。");
            UnityEngine.Object.Instantiate(prefab);
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallEditorFallback()
        {
            // 独立测试无需安装框架启动界面。
            if (UnityEditor.SessionState.GetBool("AlloyFramework.SkipAutomaticStartup", false)) return;
            // 直接从业务场景运行时也使用同一个内置界面，正式 Player 由 Boot 注册。
            EnsurePresenter();
        }
#endif
    }
}
