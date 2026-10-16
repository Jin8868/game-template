using System;
using System.IO;
using System.Text;
using System.Threading;
using AlloyFramework;
using Cysharp.Threading.Tasks;
using PulseVideoPlayer;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Video
{
    /// <summary>从本地、CDN 或 DefaultPackage 读取外置 SRT，解析后立即释放资源句柄。</summary>
    internal static class VideoSubtitleLoader
    {
        internal static async UniTask<PulseSubtitleTrack> LoadAsync(string location,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException("字幕地址不能为空。", nameof(location));
            cancellationToken.ThrowIfCancellationRequested();
            string source;
            if (Uri.TryCreate(location, UriKind.Absolute, out Uri address) &&
                (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps))
            {
                using (UnityWebRequest request = UnityWebRequest.Get(address.AbsoluteUri))
                {
                    request.timeout = ResourceSettings.OperationTimeoutSeconds;
                    await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
                    source = request.downloadHandler.text;
                }
            }
            else if (Path.IsPathRooted(location) || (address != null && address.IsFile))
            {
                string path = address != null && address.IsFile ? address.LocalPath : location;
                source = await ReadLocalAsync(path, cancellationToken);
            }
            else
            {
                if (address != null) throw new NotSupportedException("不支持的字幕协议：" + address.Scheme);
                // Unity 导入器将 .srt 转为 TextAsset，普通资源包交付文本，不混入视频 RawFile 包。
                await ResourceManager.Instance.EnsurePackageAsync(
                    ResourceSettings.DefaultPackageName, CancellationToken.None)
                    .AttachExternalCancellation(cancellationToken);
                using (IAssetHandle<TextAsset> handle = await ResourceManager.Instance.LoadAssetAsync<TextAsset>(
                    location, cancellationToken: cancellationToken))
                {
                    if (handle.Asset == null) throw new IOException("字幕 TextAsset 加载失败：" + location);
                    source = handle.Asset.text;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return PulseSubtitleTrack.Parse(source);
        }

        private static async UniTask<string> ReadLocalAsync(string path, CancellationToken cancellationToken)
        {
            // 按块读取，保留取消检查；解析后不持有文件句柄。
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var reader = new StreamReader(stream, new UTF8Encoding(false, true), true))
            {
                var builder = new StringBuilder();
                var buffer = new char[4096];
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int count = await reader.ReadAsync(buffer, 0, buffer.Length);
                    if (count == 0) return builder.ToString();
                    builder.Append(buffer, 0, count);
                }
            }
        }
    }
}
