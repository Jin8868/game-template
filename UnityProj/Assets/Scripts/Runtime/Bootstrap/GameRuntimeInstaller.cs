using UnityEngine.Scripting;

namespace Game
{
    /// <summary>
    /// 由 AOT 代码准备服务显式调用的业务安装入口。
    /// </summary>
    [Preserve]
    public static class GameRuntimeInstaller
    {
        /// <summary>
        /// 在业务代码加载后、框架配置初始化前注册业务配置加载器。
        /// </summary>
        [Preserve]
        public static void Install()
        {
            GameConfigLoaderInstaller.Install();
        }
    }
}
