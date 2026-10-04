using System.Threading;
using AlloyFramework;
using Cysharp.Threading.Tasks;
using Game.Config;

namespace Game
{
    internal sealed class GameConfigLoader : IAlloyConfigLoader
    {
        /// <summary>
        /// 全量加载游戏配置并注册业务配置表。
        /// </summary>
        /// <param name="config">用于注册配置表的框架配置入口。</param>
        /// <param name="cancellationToken">用于取消本次加载的令牌。</param>
        /// <returns>表示异步加载过程的任务。</returns>
        public async UniTask LoadAsync(AlloyConfig config, CancellationToken cancellationToken)
        {
            // 启动阶段当前只有一张表，后续可在此继续注册其他配置组。
            await GameConfig.LoadStartupAsync(cancellationToken);
            config.Register(new UIJumpConfigTableProvider(GameConfig.StartupTables.UIJumpConfigTable));
        }

        /// <summary>
        /// 释放游戏配置加载器持有的资源。
        /// </summary>
        public void Unload()
        {
            GameConfig.UnloadStartup();
        }
    }
}
