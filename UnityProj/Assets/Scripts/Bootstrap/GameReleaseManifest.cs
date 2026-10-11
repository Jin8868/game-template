using System;

namespace Game
{
    /// <summary>一次不可变发布的描述，由启动层及编辑器构建共享。</summary>
    [Serializable]
    public sealed class GameReleaseManifest
    {
        /// <summary>Unity 目标平台名称。</summary>
        public string Platform;
        /// <summary>主包裁剪基线标识。</summary>
        public string BaselineID;
        /// <summary>不可变发布目录标识。</summary>
        public string ReleaseID;
        /// <summary>本次发布固定使用的包版本。</summary>
        public GameReleasePackage[] Packages;
    }

    /// <summary>发布中的单个 YooAsset 包。</summary>
    [Serializable]
    public sealed class GameReleasePackage
    {
        /// <summary>资源包名称。</summary>
        public string Name;
        /// <summary>资源包清单版本。</summary>
        public string Version;
    }
}
