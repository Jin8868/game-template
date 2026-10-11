using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using YooAsset.Editor;

namespace Game.Editor
{
    internal static class GameBuildFingerprint
    {
        private const string REVISION = "GameBuildPipelineV2"; // 指纹规则变化时使旧缓存失效。

        [Serializable]
        private sealed class CodeSnapshot
        {
            /// <summary>构建模式和工具版本上下文。</summary>
            public string Context;
            /// <summary>全部输入的内容指纹。</summary>
            public string Hash;
            /// <summary>逐文件输入清单，用于报告差异。</summary>
            public CodeInputFile[] Files;
        }

        [Serializable]
        private sealed class CodeInputFile
        {
            /// <summary>统一分隔符后的文件路径。</summary>
            public string Path;
            /// <summary>排除已知构建临时状态后的内容指纹。</summary>
            public string Hash;
        }

        internal static string Code(bool includeBusiness)
        {
            return CaptureCode(includeBusiness).Hash;
        }

        internal static string SaveCodeSnapshot(string directory, bool includeBusiness)
        {
            CodeSnapshot snapshot = CaptureCode(includeBusiness);
            File.WriteAllText(Path.Combine(directory, GetSnapshotName(includeBusiness)),
                JsonUtility.ToJson(snapshot, true));
            return snapshot.Hash;
        }

        internal static string DescribeCodeChanges(string directory, bool includeBusiness)
        {
            string path = Path.Combine(directory, GetSnapshotName(includeBusiness));
            if (!File.Exists(path))
            {
                return "旧准备结果没有逐文件输入记录，请通过一键完整出包生成新记录。";
            }
            var previous = JsonUtility.FromJson<CodeSnapshot>(File.ReadAllText(path));
            if (previous == null || previous.Files == null)
            {
                return "逐文件输入记录不完整，请通过一键完整出包重新准备。";
            }
            CodeSnapshot current = CaptureCode(includeBusiness);
            var changes = new List<string>();
            if (previous.Context != current.Context)
            {
                changes.Add($"构建上下文改变：{previous.Context} → {current.Context}");
            }
            var previousFiles = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (CodeInputFile file in previous.Files)
            {
                previousFiles.Add(file.Path, file.Hash);
            }
            foreach (CodeInputFile file in current.Files)
            {
                if (!previousFiles.TryGetValue(file.Path, out string hash))
                {
                    changes.Add("新增：" + file.Path);
                }
                else if (hash != file.Hash)
                {
                    changes.Add("修改：" + file.Path);
                }
                previousFiles.Remove(file.Path);
            }
            foreach (string removed in previousFiles.Keys)
            {
                changes.Add("移除：" + removed);
            }
            return changes.Count == 0 ? "逐文件输入未变化，请检查指纹算法版本。" : string.Join("\n", changes);
        }

        private static string GetSnapshotName(bool includeBusiness)
        {
            return includeBusiness ? "code-input-files.json" : "aot-input-files.json";
        }

        private static CodeSnapshot CaptureCode(bool includeBusiness)
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            string business = GameHotUpdateSettings.Load().RuntimeAssemblyName;
            foreach (UnityEditor.Compilation.Assembly assembly in
                CompilationPipeline.GetAssemblies(AssembliesType.Player))
            {
                if (!includeBusiness && assembly.name == business)
                {
                    continue;
                }
                foreach (string source in assembly.sourceFiles)
                {
                    AddIfExists(paths, source);
                    AddIfExists(paths, source + ".meta");
                }
                foreach (string reference in assembly.compiledAssemblyReferences)
                {
                    if (!Path.GetFullPath(reference).Contains(Path.DirectorySeparatorChar + "Library" +
                        Path.DirectorySeparatorChar))
                    {
                        AddIfExists(paths, reference);
                    }
                }
            }
            // 代码之外的程序集边界、裁剪规则、插件和内置配置也会改变 AOT。
            foreach (string path in Directory.GetFiles("Assets", "*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".asmdef" || extension == ".asmref" || extension == ".rsp" ||
                    extension == ".dll" || extension == ".so" || extension == ".a" || extension == ".aar" ||
                    Path.GetFileName(path) == "link.xml")
                {
                    AddIfExists(paths, path);
                    AddIfExists(paths, path + ".meta");
                }
            }
            AddDirectory(paths, "Assets/Resources");
            paths.Remove("Assets/Resources/AlloyHotUpdateIdentity.json");
            paths.Remove("Assets/Resources/AlloyHotUpdateIdentity.json.meta");
            AddDirectory(paths, "ProjectSettings");
            AddIfExists(paths, "Packages/manifest.json");
            AddIfExists(paths, "Packages/packages-lock.json");
            AddIfExists(paths, "Assets/HybridCLRGenerate/HybridCLRSettings.asset");
            string context = BuildContext();
            var files = new List<CodeInputFile>();
            var content = new StringBuilder(REVISION).Append('\n').Append(context);
            foreach (string path in paths)
            {
                string normalizedPath = path.Replace('\\', '/');
                string hash = InputFileHash(path);
                files.Add(new CodeInputFile { Path = normalizedPath, Hash = hash });
                content.Append('\n').Append(normalizedPath).Append(':').Append(hash);
            }
            return new CodeSnapshot { Context = context, Hash = TextHash(content.ToString()), Files = files.ToArray() };
        }

        internal static string Package(string packageName)
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            AssetBundleCollectorPackage package = AssetBundleCollectorSettingData.Setting.GetPackage(packageName);
            foreach (AssetBundleCollectorGroup group in package.Groups)
            {
                foreach (AssetBundleCollector collector in group.Collectors)
                {
                    if (Directory.Exists(collector.CollectPath))
                    {
                        AddDirectory(paths, collector.CollectPath);
                    }
                    else
                    {
                        AddIfExists(paths, collector.CollectPath);
                        AddIfExists(paths, collector.CollectPath + ".meta");
                    }
                }
            }
            // 记录资源的间接依赖，防止材质、图集或共享配置变化后误跳过。
            var assets = new List<string>();
            foreach (string path in paths)
            {
                if (!path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    assets.Add(path.Replace('\\', '/'));
                }
            }
            if (packageName == "DefaultPackage")
            {
                foreach (string dependency in AssetDatabase.GetDependencies(assets.ToArray(), true))
                {
                    AddIfExists(paths, dependency);
                    AddIfExists(paths, dependency + ".meta");
                }
            }
            AddDirectory(paths, "Assets/Settings/YooAsset");
            foreach (UnityEditor.Compilation.Assembly assembly in
                CompilationPipeline.GetAssemblies(AssembliesType.Editor))
            {
                if ((assembly.flags & UnityEditor.Compilation.AssemblyFlags.EditorAssembly) == 0)
                {
                    continue;
                }
                foreach (string source in assembly.sourceFiles)
                {
                    AddIfExists(paths, source);
                }
            }
            AddDirectory(paths, "ProjectSettings");
            AddIfExists(paths, "Packages/packages-lock.json");
            return HashFiles(paths, BuildContext() + GamePackageBuildService.GetOptions(packageName));
        }

        internal static string Player()
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            AddDirectory(paths, "Assets/StreamingAssets");
            AddIfExists(paths, "Assets/Resources/AlloyHotUpdateIdentity.json");
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled)
                {
                    continue;
                }
                foreach (string dependency in AssetDatabase.GetDependencies(scene.path, true))
                {
                    AddIfExists(paths, dependency);
                    AddIfExists(paths, dependency + ".meta");
                }
            }
            return HashFiles(paths, Code(true));
        }

        internal static string DirectoryHash(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return string.Empty;
            }
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                // StreamingAssets 导入时产生的 meta 不参与运行时原始文件内容比较。
                if (!path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    paths.Add(path);
                }
            }
            return HashFiles(paths, string.Empty);
        }

        internal static string FileHash(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string HashFiles(IEnumerable<string> paths, string context)
        {
            var content = new StringBuilder(REVISION).Append('\n').Append(context);
            foreach (string path in paths)
            {
                content.Append('\n').Append(path.Replace('\\', '/')).Append(':').Append(InputFileHash(path));
            }
            return TextHash(content.ToString());
        }

        private static string InputFileHash(string path)
        {
            string pluginRoot = Path.GetFullPath("Assets/Wwise/API/Runtime/Plugins") + Path.DirectorySeparatorChar;
            if (!path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFullPath(path).StartsWith(pluginRoot, StringComparison.OrdinalIgnoreCase))
            {
                return FileHash(path);
            }
            // Wwise 在构建前后自动切换部署平台的 enabled 标记，该临时状态不属于代码输入。
            // 仅规范这些标记，GUID、CPU、其他导入设置和 DLL 原始内容仍参与检查。
            string source = File.ReadAllText(path);
            string normalized = Regex.Replace(source,
                @"(?m)(^  - first:\r?\n      (?!Any:|Editor:)[^\r\n]+\r?\n" +
                @"    second:\r?\n      enabled: )[01]", "$1<deployment>");
            return TextHash(normalized);
        }

        private static string TextHash(string value)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string BuildContext()
        {
            return string.Join("|", Application.unityVersion, EditorUserBuildSettings.activeBuildTarget,
                EditorUserBuildSettings.development, EditorUserBuildSettings.allowDebugging,
                EditorUserBuildSettings.connectProfiler, EditorUserBuildSettings.buildAppBundle,
                EditorUserBuildSettings.exportAsGoogleAndroidProject, GetWwiseConfiguration());
        }

        private static string GetWwiseConfiguration()
        {
            // 部署选择由 Wwise 配置决定，不能靠临时启用标记判断 Profile 或 Release 是否变化。
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type activator = assembly.GetType("AkPluginActivator", false);
                MethodInfo method = activator?.GetMethod("GetCurrentConfig", BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    return (string)method.Invoke(null, null);
                }
            }
            return "WwiseNotLoaded";
        }

        private static void AddDirectory(SortedSet<string> paths, string directory)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }
            foreach (string path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                paths.Add(path.Replace('\\', '/'));
            }
        }

        private static void AddIfExists(SortedSet<string> paths, string path)
        {
            if (File.Exists(path))
            {
                paths.Add(path.Replace('\\', '/'));
            }
        }
    }
}
