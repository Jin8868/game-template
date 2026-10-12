using System;
using System.Collections.Generic;
using System.Threading;
using AlloyFramework;
using AlloyFramework.HybridCLR;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Game
{
    internal static class GameRemoteRelease
    {
        private static GameHotUpdateSettings m_settings; // 启动前读取的 AOT 配置。
        private static GameReleaseManifest m_release; // 本次启动固定的发布描述。
        private static string m_baselineURL; // 当前平台、渠道和主包基线的根地址。

        internal static void Configure(GameHotUpdateSettings settings)
        {
            // 显式清理设置，支持关闭域重载后的重复编辑器运行。
            m_settings = settings;
            m_release = null;
            m_baselineURL = null;
            ResourceSettings.StartupPreparation = null;
            ResourceSettings.PackageVersionProvider = null;
            ResourceSettings.RemoteUrlProvider = null;
#if UNITY_EDITOR
            bool host = settings.ResourceMode == EGameResourceMode.HotUpdate && settings.EditorHostMode;
            ResourceSettings.PlayModeOverride = host ? ResourcePlayMode.Host : ResourcePlayMode.EditorSimulate;
#else
            bool host = settings.ResourceMode == EGameResourceMode.HotUpdate;
            ResourceSettings.PlayModeOverride = host ? ResourcePlayMode.Host : ResourcePlayMode.Offline;
#endif
            if (host)
            {
                ResourceSettings.StartupPreparation = PrepareAsync;
                ResourceSettings.PackageVersionProvider = GetPackageVersion;
                ResourceSettings.RemoteUrlProvider = GetRemoteURL;
            }
        }

        private static async UniTask PrepareAsync(CancellationToken cancellationToken)
        {
            TextAsset asset = Resources.Load<TextAsset>("AlloyHotUpdateIdentity");
            HotUpdatePlayerIdentity identity = asset == null ? null :
                JsonUtility.FromJson<HotUpdatePlayerIdentity>(asset.text);
            if (identity == null || !GameHotUpdateSettings.IsPathSegment(identity.Platform) ||
                !GameHotUpdateSettings.IsPathSegment(identity.BaselineID))
            {
                throw new InvalidOperationException("主包身份缺失，请通过一键主包工具构建。");
            }
            m_baselineURL = m_settings.ServerURL + "/" + identity.Platform + "/" +
                m_settings.Channel + "/" + identity.BaselineID;
            string url = m_baselineURL + "/release.json";
            StartupLoading.ReportOperation(EStartupLoadingStage.CheckingRelease, "检查热更新版本");
            for (int attempt = 0; attempt <= m_settings.RequestRetryCount; attempt++)
            {
                // 每次重试独立请求；取消时释放请求并终止网络传输。
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    request.timeout = m_settings.RequestTimeoutSeconds;
                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                    try
                    {
                        while (!operation.isDone)
                        {
                            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            GameReleaseManifest release = JsonUtility.FromJson<GameReleaseManifest>(
                                request.downloadHandler.text);
                            ValidateRelease(release, identity);
                            m_release = release;
                            Debug.Log($"[HotUpdate] 已固定发布 {release.ReleaseID}，基线={release.BaselineID}");
                            return;
                        }
                        if (attempt == m_settings.RequestRetryCount)
                        {
                            throw new InvalidOperationException($"发布信息请求失败：{url}，{request.error}");
                        }
                    }
                    finally
                    {
                        if (!operation.isDone)
                        {
                            request.Abort();
                        }
                    }
                }
                await UniTask.Delay(1000, cancellationToken: cancellationToken);
            }
        }

        private static void ValidateRelease(GameReleaseManifest release, HotUpdatePlayerIdentity identity)
        {
            if (release == null || release.Platform != identity.Platform || release.BaselineID != identity.BaselineID ||
                !GameHotUpdateSettings.IsPathSegment(release.ReleaseID) || release.Packages == null)
            {
                throw new InvalidOperationException("远端发布的平台或基线与当前主包不匹配。");
            }
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameReleasePackage package in release.Packages)
            {
                if (package == null || !GameHotUpdateSettings.IsPathSegment(package.Name) ||
                    !GameHotUpdateSettings.IsPathSegment(package.Version) || !names.Add(package.Name))
                {
                    throw new InvalidOperationException("远端发布包含非法包名、版本或重复包。");
                }
            }
            if (!names.Contains(ResourceSettings.DefaultPackageName) || !names.Contains("AudioPackage") ||
                (m_settings.CodeMode == EGameCodeMode.HybridCLR && !names.Contains("CodePackage")))
            {
                throw new InvalidOperationException("远端发布缺少当前主包必需的资源包。");
            }
        }

        private static string GetPackageVersion(string packageName)
        {
            if (m_release != null)
            {
                foreach (GameReleasePackage package in m_release.Packages)
                {
                    if (package.Name == packageName)
                    {
                        return package.Version;
                    }
                }
            }
            throw new InvalidOperationException($"本次发布没有指定资源包版本：{packageName}");
        }

        private static string GetRemoteURL(string packageName, string fileName)
        {
            GetPackageVersion(packageName);
            return m_baselineURL + "/releases/" + m_release.ReleaseID + "/" + packageName + "/" +
                Uri.EscapeDataString(fileName);
        }
    }
}
