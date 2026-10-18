using UnityEngine;

namespace Game
{
    /// <summary>Boot 场景中的共享启动配置入口。</summary>
    public sealed class GameBootConfiguration : MonoBehaviour
    {
        [SerializeField]
        private GameHotUpdateSettings m_settings; // 与 Resources 启动和编辑器构建共用的配置资产。

        public GameHotUpdateSettings Settings => m_settings;

        private void Awake()
        {
            // 场景入口再次确保日志存在，不依赖同一启动阶段的回调执行顺序。
            GameLogViewerInstaller.Install();

            // 在 Boot 的 Awake 注册 AOT 展示，框架异步等待其实际绘制完成。
            GameStartupLoadingInstaller.EnsurePresenter();
        }
    }
}
