using System;
using UnityEngine;
using AlloyFramework;
using AlloyFramework.Audio;
using AlloyFramework.HybridCLR;

namespace Game
{
    /// <summary>
    /// 主包内嵌的热更新入口配置，由 AOT 启动和编辑器构建共同读取。
    /// </summary>
    [CreateAssetMenu(menuName = "★AlloyFramework★/Boot 启动配置")]
    public sealed class GameHotUpdateSettings : FrameworkBootSettings
    {
        private const string RESOURCELOCATION = "GameHotUpdateSettings"; // 构建工具使用的共用配置地址。
        private const string RUNTIMEASSEMBLYNAME = "Game.Runtime"; // 运行与构建共用的业务程序集约定。
        private const string INSTALLERTYPENAME = "Game.GameRuntimeInstaller"; // AOT 层按名称定位的业务安装入口。

        /// <summary>构建工具使用的固定配置地址。</summary>
        public override string BuildConfigurationResourcePath => RESOURCELOCATION;

        /// <summary>固定的业务程序集名称，不含 DLL 扩展名。</summary>
        public override string RuntimeAssemblyName => RUNTIMEASSEMBLYNAME;
        /// <summary>固定的业务安装入口完整类型名称。</summary>
        public override string InstallerTypeName => INSTALLERTYPENAME;
        /// <summary>
        /// 读取主包内嵌的热更新入口配置并检查必填信息。
        /// </summary>
        /// <returns>供启动和构建共同使用的入口配置。</returns>
        /// <exception cref="InvalidOperationException">配置缺失、服务器参数或运行模式不合法。</exception>
        public static GameHotUpdateSettings Load()
        {
            // 配置必须在业务 DLL 加载前可用，不能由业务配置系统提供。
            GameHotUpdateSettings settings = Resources.Load<GameHotUpdateSettings>(RESOURCELOCATION);
            if (settings == null)
            {
                throw new InvalidOperationException("缺少 Resources/GameHotUpdateSettings 热更新入口配置。");
            }
            settings.Validate();
            return settings;
        }

        /// <summary>由框架启动步骤显式读取主包配置，并声明后续步骤需要创建的实现类型。</summary>
        /// <returns>本轮启动使用的实现类型与业务入口信息。</returns>
        /// <exception cref="InvalidOperationException">主包启动配置不合法。</exception>
        public override GameStartupConfiguration CreateStartupConfiguration()
        {
            // 沿用构建和启动共用的配置校验，资源模式必须早于 Resource 初始化确定。
            Validate();
            GameRemoteRelease.Configure(this);
            AudioManager.Instance.SetInstaller(GameAudioInstaller.InstallAsync);

            Type preparationType = CodeMode == EGameCodeMode.HybridCLR
                ? typeof(HybridCLRCodePreparation)
                : typeof(DefaultGameCodePreparation);
            return new GameStartupConfiguration(
                preparationType,
                RuntimeAssemblyName,
                InstallerTypeName);
        }

    }
}
