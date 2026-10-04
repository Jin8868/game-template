using System.Threading;
using AlloyFramework.UI;
using Cysharp.Threading.Tasks;
using Game.UI;

namespace Game
{
    internal sealed class ConfigureFrameworkExtensionsStep : IGameStartupStep
    {
        /// <summary>当前启动步骤的日志名称。</summary>
        public string Name => "配置框架业务扩展";

        /// <summary>
        /// 安装业务 UI 定义提供器和 Luban 导航配置适配器。
        /// </summary>
        /// <param name="cancellationToken">用于取消启动流程的令牌。</param>
        /// <returns>表示扩展安装过程的任务。</returns>
        public UniTask ExecuteAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // UI 定义按名称延迟创建，导航配置则在启动阶段一次性完成静态校验。
            UIManager.Instance.SetDefinitionProvider(GameUI.DefinitionProvider);
            UIManager.Instance.ConfigureNavigation(
                new LubanUIJumpConfigProvider(GameConfig.StartupTables.UIJumpConfigTable));
            return UniTask.CompletedTask;
        }
    }
}
