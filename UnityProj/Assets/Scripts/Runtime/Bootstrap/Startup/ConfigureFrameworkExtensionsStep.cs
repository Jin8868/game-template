using System.Threading;
using AlloyFramework;
using AlloyFramework.UI;
using Cysharp.Threading.Tasks;
using Game.UI;

namespace Game
{
    internal sealed class ConfigureFrameworkExtensionsStep : IGameStartupStep
    {
        /// <summary>当前启动步骤的日志名称。</summary>
        public string Name => "配置框架业务扩展";

        /// <summary>当前步骤占用一个真实 Loading 步骤。</summary>
        public int StepCount => 1;

        /// <summary>
        /// 安装业务 UI 定义字典和 Luban 导航配置适配器。
        /// </summary>
        /// <param name="loadingSession">共享的 Loading 会话；无展示时可为空。</param>
        /// <param name="cancellationToken">用于取消启动流程的令牌。</param>
        /// <returns>表示扩展安装过程的任务。</returns>
        public UniTask ExecuteAsync(
            IStartupLoadingSession loadingSession,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 由当前业务步骤报告实际执行阶段。
            loadingSession?.BeginStep(EStartupLoadingStage.InitializingBusiness, Name);

            // 先安装启动时构建的 UI 定义字典，再一次性校验全部导航配置引用。
            UIManager.Instance.SetDefinitionProvider(GameUI.DefinitionProvider);
            UIManager.Instance.ConfigureNavigation(
                new LubanUIJumpConfigProvider(GameConfig.StartupTables.UIJumpConfigTable));
            cancellationToken.ThrowIfCancellationRequested();
            loadingSession?.CompleteStep();
            return UniTask.CompletedTask;
        }
    }
}
