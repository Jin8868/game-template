using System;
using UnityEngine;
using AlloyFramework;
using AlloyFramework.Audio;
using AlloyFramework.HybridCLR;

namespace Game
{
    /// <summary>主包中业务代码的执行方式。</summary>
    public enum EGameCodeMode
    {
        Default,
        HybridCLR
    }

    /// <summary>Player 的资源来源。</summary>
    public enum EGameResourceMode
    {
        Offline,
        HotUpdate
    }

    /// <summary>
    /// 主包内嵌的热更新入口配置，由 AOT 启动和编辑器构建共同读取。
    /// </summary>
    [CreateAssetMenu(menuName = "★AlloyFramework★/Boot 启动配置")]
    public sealed class GameHotUpdateSettings : ScriptableObject, IGameStartupConfigurationProvider
    {
        private const string RESOURCELOCATION = "GameHotUpdateSettings";

        [Header("Boot 运行模式")]
        [SerializeField]
        private EGameCodeMode m_codeMode = EGameCodeMode.HybridCLR; // 默认从代码资源包加载业务 DLL。
        [SerializeField]
        private EGameResourceMode m_resourceMode; // 默认保留无需服务器的离线启动。
        [SerializeField]
        private bool m_editorHostMode; // 编辑器是否测试远端资源，业务代码仍复用编辑器程序集。
        [Header("远端发布")]
        [SerializeField]
        private string m_serverURL = "http://127.0.0.1:8080"; // 本地测试服务器，可改为真机可访问的地址。
        [SerializeField]
        private string m_channel = "local"; // 按渠道隔离发布指针。
        [SerializeField, Min(1)]
        private int m_requestTimeoutSeconds = 30; // 发布描述请求的超时秒数。
        [SerializeField, Min(0)]
        private int m_requestRetryCount = 2; // 发布描述请求失败后的重试次数。
        [Header("业务入口")]
        [SerializeField]
        private string m_runtimeAssemblyName; // 提供唯一业务入口的程序集名称，不含 DLL 扩展名。
        [SerializeField]
        private string m_installerTypeName; // 业务显式安装入口的完整类型名称。

        public string RuntimeAssemblyName => m_runtimeAssemblyName;
        public string InstallerTypeName => m_installerTypeName;
        public EGameCodeMode CodeMode => m_codeMode;
        public EGameResourceMode ResourceMode => m_resourceMode;
        public bool EditorHostMode => m_editorHostMode;
        public string ServerURL => m_serverURL.TrimEnd('/');
        public string Channel => m_channel;
        public int RequestTimeoutSeconds => m_requestTimeoutSeconds;
        public int RequestRetryCount => m_requestRetryCount;

        /// <summary>
        /// 读取主包内嵌的热更新入口配置并检查必填信息。
        /// </summary>
        /// <returns>供启动和构建共同使用的入口配置。</returns>
        /// <exception cref="InvalidOperationException">配置缺失或入口名称不合法。</exception>
        public static GameHotUpdateSettings Load()
        {
            // 配置必须在业务 DLL 加载前可用，不能由业务配置系统提供。
            GameHotUpdateSettings settings = Resources.Load<GameHotUpdateSettings>(RESOURCELOCATION);
            if (settings == null)
            {
                throw new InvalidOperationException("缺少 Resources/GameHotUpdateSettings 热更新入口配置。");
            }
            if (string.IsNullOrWhiteSpace(settings.m_runtimeAssemblyName) ||
                settings.m_runtimeAssemblyName != settings.m_runtimeAssemblyName.Trim() ||
                settings.m_runtimeAssemblyName.IndexOfAny(new[] { '/', '\\', ':', '"' }) >= 0 ||
                settings.m_runtimeAssemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(settings.m_installerTypeName) ||
                settings.m_installerTypeName != settings.m_installerTypeName.Trim())
            {
                throw new InvalidOperationException("热更新入口配置中的程序集名称或完整安装类型名称不合法。");
            }
            // 发布路径只允许单段标识，防止配置逃逸出当前渠道目录。
            if (!IsPathSegment(settings.m_channel) || settings.m_requestTimeoutSeconds < 1 ||
                settings.m_requestRetryCount < 0 ||
                settings.m_serverURL != settings.m_serverURL?.Trim() ||
                !Uri.TryCreate(settings.m_serverURL, UriKind.Absolute, out Uri server) ||
                (server.Scheme != Uri.UriSchemeHttp && server.Scheme != Uri.UriSchemeHttps) ||
                !string.IsNullOrEmpty(server.Query) || !string.IsNullOrEmpty(server.Fragment))
            {
                throw new InvalidOperationException("Boot 服务器地址、渠道或请求参数不合法。");
            }
            if (!Enum.IsDefined(typeof(EGameCodeMode), settings.m_codeMode) ||
                !Enum.IsDefined(typeof(EGameResourceMode), settings.m_resourceMode))
            {
                throw new InvalidOperationException("Boot 代码模式或资源模式不合法。");
            }
            return settings;
        }

        /// <summary>由框架启动步骤显式读取主包配置，并声明后续步骤需要创建的实现类型。</summary>
        /// <returns>本轮启动使用的实现类型与业务入口信息。</returns>
        /// <exception cref="InvalidOperationException">主包启动配置不合法。</exception>
        public GameStartupConfiguration CreateStartupConfiguration()
        {
            // 沿用构建和启动共用的配置校验，资源模式必须早于 Resource 初始化确定。
            GameHotUpdateSettings settings = Load();
            GameRemoteRelease.Configure(settings);
            AudioManager.Instance.SetInstaller(GameAudioInstaller.InstallAsync);

            Type preparationType = settings.CodeMode == EGameCodeMode.HybridCLR
                ? typeof(HybridCLRCodePreparation)
                : typeof(DefaultGameCodePreparation);
            Type loaderType = settings.CodeMode == EGameCodeMode.HybridCLR
                ? typeof(HybridCLRGameEntryLoader)
                : typeof(DefaultGameEntryLoader);
            return new GameStartupConfiguration(
                preparationType,
                loaderType,
                settings.RuntimeAssemblyName,
                settings.InstallerTypeName);
        }

        /// <summary>检查标识是否可以安全用作发布路径中的单段目录。</summary>
        /// <param name="value">待检查的标识。</param>
        /// <returns>标识非空且只包含允许字符时返回 true。</returns>
        public static bool IsPathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "." || value == "..")
            {
                return false;
            }
            foreach (char character in value)
            {
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_' && character != '.')
                {
                    return false;
                }
            }
            return true;
        }
    }
}
