using AlloyFramework;

namespace Game
{
    internal static class GameConfigLoaderInstaller
    {
        internal static void Install()
        {
            // 由业务程序集显式安装入口调用，不能依赖动态 DLL 的 Unity 初始化属性。
            AlloyConfig.Instance.SetLoader(new GameConfigLoader());
        }
    }
}
