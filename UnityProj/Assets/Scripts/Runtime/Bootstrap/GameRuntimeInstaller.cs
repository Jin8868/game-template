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
        /// 在业务代码加载后注册配置加载器；业务初始化器由后续框架步骤反射创建。
        /// </summary>
        [Preserve]
        public static void Install()
        {
            // 此处只注册；业务初始化必须等框架系统全部就绪后执行。
            GameConfigLoaderInstaller.Install();
        }
    }
}
