namespace Game
{
    /// <summary>业务 Model 和 UIModel 的统一注册入口。</summary>
    public static class GameModelRegistration
    {
        /// <summary>
        /// 注册全部业务 Model 类型；只登记类型，首次 Get 时才创建和初始化。
        /// </summary>
        public static void RegisterModels()
        {
            // 新增业务 Model 后在此注册，例如：ModelManager.Instance.Register<PlayerModel>();
            // 所有注册类型必须继承 AlloyFramework.ModelBase 并提供公开的无参构造函数。
        }
    }
}
