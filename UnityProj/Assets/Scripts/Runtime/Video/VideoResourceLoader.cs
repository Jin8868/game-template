using System;
using System.IO;
using System.Threading;
using AlloyFramework;
using Cysharp.Threading.Tasks;
using PulseVideoPlayer;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Video
{
    /// <summary>将 YooAssets 资源、CDN 视频和本地文件统一交付为文件租约。</summary>
    internal static class VideoResourceLoader
    {
        internal static async UniTask<VideoResourceLease> LoadAsync(string location, string packageName,
            IProgress<float> progress, CancellationToken cancellationToken)
        {
            return await LoadAsync(location, packageName, progress, cancellationToken, null);
        }

        internal static async UniTask<VideoResourceLease> LoadAsync(string location, string packageName,
            IProgress<float> progress, CancellationToken cancellationToken, PulsePreparationTimings timings)
        {
            timings?.BeginStage("视频地址解析");
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException("视频地址不能为空。", nameof(location));
#if UNITY_ANDROID && !UNITY_EDITOR
            if (location.StartsWith("jar:", StringComparison.OrdinalIgnoreCase))
            {
                // APK 内置视频由 Pulse 按 fd 区间直读，不能当作普通文件或整包拷出。
                return new VideoResourceLease(location);
            }
#endif
            if (Uri.TryCreate(location, UriKind.Absolute, out Uri address))
            {
                if (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps)
                {
                    timings?.BeginStage("CDN下载及文件校验");
                    return await DownloadAsync(address.AbsoluteUri, progress, cancellationToken);
                }
                if (address.IsFile) return LoadLocal(address.LocalPath);
                if (!Path.IsPathRooted(location)) throw new NotSupportedException("不支持的视频协议：" + address.Scheme);
            }
            if (Path.IsPathRooted(location)) return LoadLocal(location);

            // 先准备包，再借用原始文件；租约保持到原生解码和 GPU 退役完成。
            // 包初始化是共享状态；界面关闭只取消等待，不中断后续界面也要使用的包初始化。
            var trace = new VideoPlaybackTrace("YooAssets 视频");
            try
            {
                timings?.BeginStage("VideoPackage就绪");
                await ResourceManager.Instance.EnsurePackageAsync(packageName, CancellationToken.None)
                    .AttachExternalCancellation(cancellationToken);
#if UNITY_ANDROID && !UNITY_EDITOR
                timings?.BeginStage("APK视频来源检查");
                cancellationToken.ThrowIfCancellationRequested();
                if (VideoBuildinAddressResolver.TryGetAddress(
                    location, packageName, out string buildinAddress, out string fallbackReason))
                {
                    timings?.AddDetail("视频来源", "APK直读，无整文件落盘");
                    trace.Complete();
                    return new VideoResourceLease(buildinAddress);
                }
                timings?.AddDetail("视频直读回退", fallbackReason);
#endif
                timings?.AddDetail("视频来源", "YooAsset原始文件租约");
                timings?.BeginStage("RawFile加载及租约");
                RawFileLease rawFile = await ResourceManager.Instance.LoadRawFileAsync(
                    location, packageName, cancellationToken);
                trace.Complete();
                return new VideoResourceLease(rawFile.FilePath, rawFile);
            }
            catch (Exception exception) { trace.Fail(exception); throw; }
        }

        private static VideoResourceLease LoadLocal(string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("视频文件不存在。", filePath);
            return new VideoResourceLease(Path.GetFullPath(filePath));
        }

        private static async UniTask<VideoResourceLease> DownloadAsync(string url, IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            string directory = Path.Combine(Application.persistentDataPath, "PulseDownloads",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string filePath = Path.Combine(directory, "video.pulse");
            var trace = new VideoPlaybackTrace("CDN 视频");
            try
            {
                // 文件直写磁盘，不把长视频整体载入托管内存。取消和失败均移除本次下载。
                using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET))
                {
                    request.downloadHandler = new DownloadHandlerFile(filePath) { removeFileOnAbort = true };
                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                    ulong previousBytes = 0;
                    double lastProgressTime = Time.realtimeSinceStartupAsDouble;
                    while (!operation.isDone)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (request.downloadedBytes != previousBytes)
                        {
                            previousBytes = request.downloadedBytes;
                            lastProgressTime = Time.realtimeSinceStartupAsDouble;
                        }
                        if (Time.realtimeSinceStartupAsDouble - lastProgressTime >
                            ResourceSettings.OperationTimeoutSeconds)
                            throw new TimeoutException("视频下载长时间没有数据：" + url);
                        if (request.downloadProgress >= 0) progress?.Report(request.downloadProgress);
                        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    if (request.result != UnityWebRequest.Result.Success)
                        throw new IOException($"视频下载失败 HTTP {request.responseCode}：{request.error}");
                }
                // 校验 Pulse 元数据，避免把 CDN 错误页交给原生解码器。
                await PulseMetadataLoader.ReadAsync(filePath, cancellationToken);
                progress?.Report(1);
                trace.Complete();
                return new VideoResourceLease(filePath, ownedDirectory: directory);
            }
            catch (Exception exception)
            {
                trace.Fail(exception);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
                throw;
            }
        }
    }
}
