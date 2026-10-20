using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AlloyFramework.Audio;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>只为显式配置的视频事件生成可被 Wwise 从 APK 按需读取的媒体文件包。</summary>
    internal static class GameVideoAudioPackageBuilder
    {
        private const string SETTINGSLOCATION = "Assets/Settings/VideoAudioStreaming.json"; // 视频长音轨白名单。
        private const string MANIFESTLOCATION =
            "Assets/Res/WwiseAudio/Android/AlloyAudioManifest.json"; // 由音频导出流程生成的当前平台清单。
        private const int DATAOFFSET = 4096; // 文件包数据对齐，头部索引不包含媒体载荷。

        [Serializable]
        private sealed class Settings
        {
            /// <summary>需要独立媒体文件包的视频音频事件。</summary>
            public string[] Events;
        }

        internal static void Prepare()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android ||
                !File.Exists(SETTINGSLOCATION)) return;
            Settings settings = JsonUtility.FromJson<Settings>(File.ReadAllText(SETTINGSLOCATION));
            AudioContentManifest manifest = JsonUtility.FromJson<AudioContentManifest>(
                File.ReadAllText(MANIFESTLOCATION));
            if (settings?.Events == null || manifest?.Groups == null || manifest.Events == null)
                throw new InvalidOperationException("视频音轨配置或音频清单不完整。");

            var groups = new Dictionary<string, AudioContentGroup>(StringComparer.Ordinal);
            foreach (AudioContentGroup group in manifest.Groups) groups.Add(group.Key, group);
            var selected = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Queue<string>();
            foreach (string eventKey in settings.Events)
            {
                bool found = false;
                foreach (AudioEventDependency dependency in manifest.Events)
                {
                    if (dependency.Key != eventKey) continue;
                    found = true;
                    foreach (string groupKey in dependency.Groups) pending.Enqueue(groupKey);
                }
                if (!found) throw new InvalidOperationException("视频音频事件未导出：" + eventKey);
            }
            while (pending.Count > 0)
            {
                string groupKey = pending.Dequeue();
                if (!selected.Add(groupKey)) continue;
                AudioContentGroup group = groups[groupKey];
                if (group.Dependencies == null) continue;
                foreach (string dependencyKey in group.Dependencies) pending.Enqueue(dependencyKey);
            }

            // 原始媒体继续作为制作输入；Collector 只把视频媒体的文件包加入运行时资源包。
            string root = Path.GetDirectoryName(MANIFESTLOCATION);
            var packagedMedia = new Dictionary<uint, string>();
            foreach (AudioContentGroup group in manifest.Groups)
            {
                foreach (AudioContentFile file in group.Files)
                {
                    file.FilePackagePath = null;
                    file.FilePackageHash = null;
                    file.FilePackageSize = 0;
                    if (!selected.Contains(group.Key) ||
                        !file.Path.EndsWith(".wem", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!uint.TryParse(Path.GetFileNameWithoutExtension(file.Path),
                        NumberStyles.None, CultureInfo.InvariantCulture, out uint mediaID))
                        throw new InvalidOperationException("视频流媒体必须使用 Wwise 媒体 ID 文件名：" + file.Path);
                    if (packagedMedia.TryGetValue(mediaID, out string previousHash) && previousHash != file.Hash)
                        throw new InvalidOperationException("同一视频媒体 ID 对应了不同内容：" + mediaID);
                    packagedMedia[mediaID] = file.Hash;
                    string source = Path.GetFullPath(Path.Combine(root, file.Path));
                    string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
                        Path.DirectorySeparatorChar;
                    if (!source.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("音频文件超出导出目录：" + file.Path);
                    if (!string.Equals(HashFile(source), file.Hash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("音频输入与导出清单不一致：" + file.Path);
                    string relative = "StreamingPackages/" + mediaID + ".pck";
                    string destination = Path.Combine(root, relative);
                    WritePackage(source, destination, mediaID, manifest.Language);
                    file.FilePackagePath = relative;
                    file.FilePackageHash = HashFile(destination);
                    file.FilePackageSize = new FileInfo(destination).Length;
                }
            }
            // 版本包含文件包描述；先清空旧版本，避免重复构建时对版本本身递归求摘要。
            manifest.Version = null;
            using (var hash = SHA256.Create())
            {
                byte[] content = Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest));
                manifest.Version = BitConverter.ToString(hash.ComputeHash(content)).Replace("-", string.Empty);
            }
            File.WriteAllText(MANIFESTLOCATION, JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
            AssetDatabase.Refresh();
        }

        private static void WritePackage(string source, string destination, uint mediaID, string language)
        {
            // 对照随工程 SDK 提供的 AKPK v1 Header、LanguageMap 和 AkFileEntry 布局生成索引。
            if (string.IsNullOrWhiteSpace(language))
                throw new InvalidOperationException("视频音轨清单缺少语言名称。");
            long size = new FileInfo(source).Length;
            if (size <= 0 || size > uint.MaxValue - DATAOFFSET)
                throw new InvalidOperationException("视频音轨超出 AKPK v1 单文件大小范围：" + source);
            byte[] languageBytes = Encoding.UTF8.GetBytes(language.ToLowerInvariant() + '\0');
            uint languageMapSize = (uint)((12 + languageBytes.Length + 3) & ~3);
            uint headerSize = 28 + languageMapSize + 4 + 44 + 4;
            if (headerSize > DATAOFFSET) throw new InvalidOperationException("音频语言名称过长。");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string staging = destination + ".building";
            try
            {
                using (var output = new FileStream(staging, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(output, Encoding.UTF8, true))
                {
                    writer.Write(Encoding.ASCII.GetBytes("AKPK"));
                    writer.Write(headerSize - 8);
                    writer.Write(1u);
                    writer.Write(languageMapSize);
                    writer.Write(4u);
                    writer.Write(44u);
                    writer.Write(4u);
                    writer.Write(1u);
                    writer.Write(12u);
                    writer.Write(1u);
                    writer.Write(languageBytes);
                    while (output.Position < 28 + languageMapSize) writer.Write((byte)0);
                    writer.Write(0u);
                    writer.Write(2u);
                    // SFX 与当前语言共用同一媒体区间，不复制两份音频数据。
                    for (uint languageID = 0; languageID < 2; languageID++)
                    {
                        writer.Write(mediaID);
                        writer.Write(1u);
                        writer.Write((uint)size);
                        writer.Write((uint)DATAOFFSET);
                        writer.Write(languageID);
                    }
                    writer.Write(0u);
                    while (output.Position < DATAOFFSET) writer.Write((byte)0);
                    using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                        input.CopyTo(output);
                }
                if (File.Exists(destination)) File.Delete(destination);
                File.Move(staging, destination);
            }
            finally
            {
                if (File.Exists(staging)) File.Delete(staging);
            }
        }

        private static string HashFile(string path)
        {
            using (var hash = SHA256.Create())
            using (var input = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", string.Empty);
        }
    }
}
