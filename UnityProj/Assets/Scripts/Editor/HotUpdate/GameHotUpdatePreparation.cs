using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;
using AlloyFramework;
using AlloyFramework.HybridCLR;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Installer;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;
using BuildReport = UnityEditor.Build.Reporting.BuildReport;

namespace Game.Editor
{
    /// <summary>
    /// 主包代码准备和最终 AOT 基线校验，供独立菜单及一键出包共同使用。
    /// </summary>
    public sealed class GameHotUpdatePreparation : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string CODEROOT = "Assets/Res/HotUpdateCode";
        private const string IDENTITYPATH = "Assets/Resources/AlloyHotUpdateIdentity.json";
        private const string BASELINEROOT = "BuildArtifacts/AOTBaselines";
        private static bool m_isPreparing; // 跳过官方生成流程内部的临时裁剪构建。

        public int callbackOrder => 10000;

        [MenuItem("★AlloyFramework★/出包/构建配置/配置 Windows 与 Android 热更新接入")]
        internal static void Configure()
        {
            CheckEditorState();
            var installer = new InstallerController();
            if (!installer.HasInstalledHybridCLR() || installer.PackageVersion != installer.InstalledLibil2cppVersion)
            {
                throw new BuildFailedException("请先在 HybridCLR/Installer 安装与包版本一致的运行时。");
            }

            // 首期只热更新业务程序集，Bootstrap 与 Protocol 保留在 AOT 边界。
            GameHotUpdateSettings entrySettings = GameHotUpdateSettings.Load();
            ValidateEntry(entrySettings);
            HybridCLRSettings settings = HybridCLRSettings.Instance;
            settings.enable = true;
            settings.useGlobalIl2cpp = false;
            settings.hotUpdateAssemblyDefinitions = Array.Empty<UnityEditorInternal.AssemblyDefinitionAsset>();
            settings.hotUpdateAssemblies = entrySettings.CodeMode == EGameCodeMode.HybridCLR ?
                new[] { entrySettings.RuntimeAssemblyName } : Array.Empty<string>();
            settings.preserveHotUpdateAssemblies = Array.Empty<string>();
            // 官方泛型参考文件只用于查看清单，放到 Assets 外避免改变主包 AOT。
            settings.outputAOTGenericReferenceFile = "../BuildArtifacts/HybridCLRGenerated/AOTGenericReferences.cs";
            if (settings.patchAOTAssemblies == null || settings.patchAOTAssemblies.Length == 0)
            {
                settings.patchAOTAssemblies = new[]
                {
                    "mscorlib", "System", "System.Core", "Alloy.Core", "Alloy.Runtime",
                    "UniTask", "Luban.Runtime", "UnityEngine.CoreModule"
                };
            }
            HybridCLRSettings.Save();

            // Default 模式显式保留静态业务入口，HybridCLR 模式交由官方工具处理热更代码。
            const string defaultLinkPath = "Assets/HybridCLRGenerate/DefaultEntry/link.xml";
            Directory.CreateDirectory(Path.GetDirectoryName(defaultLinkPath));
            string defaultLink = entrySettings.CodeMode == EGameCodeMode.Default ?
                $"<linker><assembly fullname=\"{entrySettings.RuntimeAssemblyName}\" preserve=\"all\" /></linker>" :
                "<linker />";
            if (!File.Exists(defaultLinkPath) || File.ReadAllText(defaultLinkPath) != defaultLink)
            {
                File.WriteAllText(defaultLinkPath, defaultLink);
            }

            // 两个平台使用相同 API 环境，Android 首期选择 ARM64。
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Unity_4_8);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Android, ApiCompatibilityLevel.NET_Unity_4_8);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // Pulse 当前仅支持 GLES3；不能让自动选择优先启用 Vulkan。
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            if (entrySettings.ResourceMode == EGameResourceMode.HotUpdate &&
                entrySettings.ServerURL.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                // 本机 HTTP 验证需要 Android 主包允许明文请求；正式 HTTPS 配置无需此项。
                PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[HybridCLR] Windows x64 / Android ARM64 接入配置完成；请按目标平台准备主包代码资源。");
        }

        [MenuItem("★AlloyFramework★/出包/单项操作/单独生成主包代码与 AOT 基线")]
        internal static void PrepareMainPackage()
        {
            CheckEditorState();
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            ValidateTarget(target);
            GameHotUpdateSettings entrySettings = GameHotUpdateSettings.Load();
            ValidateEntry(entrySettings);
            bool hybrid = entrySettings.CodeMode == EGameCodeMode.HybridCLR;
            if (!HybridCLRSettings.Instance.enable || (hybrid &&
                Array.IndexOf(HybridCLRSettings.Instance.hotUpdateAssemblies ?? Array.Empty<string>(),
                    entrySettings.RuntimeAssemblyName) < 0))
            {
                throw new BuildFailedException(
                    "请先执行 ★AlloyFramework★/出包/构建配置/配置 Windows 与 Android 热更新接入，" +
                    "再等待 Unity 编译完成。");
            }
            string baselineID = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            string baselineDirectory = Path.Combine(BASELINEROOT, target.ToString(), baselineID);
            if (Directory.Exists(baselineDirectory))
            {
                throw new BuildFailedException("主包基线目录已存在，拒绝覆盖。");
            }

            try
            {
                // 官方生成包含临时 IL2CPP 裁剪构建，不等于最终 Player 出包。
                m_isPreparing = true;
                Debug.Log($"[Build][代码子步骤开始] HybridCLR GenerateAll，平台={target}，基线={baselineID}");
                PrebuildCommand.GenerateAll();
                Debug.Log("[Build][代码子步骤完成] HybridCLR GenerateAll；开始整理 DLL、AOT 元数据和主包身份。");
                UpdateMetadataList();
                var manifest = new HotUpdateCodeManifest
                {
                    Platform = target.ToString(),
                    BaselineID = baselineID,
                    ReleaseID = baselineID,
                    RuntimeAssemblyName = entrySettings.RuntimeAssemblyName,
                    InstallerTypeName = entrySettings.InstallerTypeName,
                    Assemblies = new HotUpdateCodeFile[hybrid ? 1 : 0],
                    Metadata = new HotUpdateCodeFile[hybrid ? HybridCLRSettings.Instance.patchAOTAssemblies.Length : 0]
                };
                Directory.CreateDirectory(Path.Combine(CODEROOT, "AOTMetadata"));
                Directory.CreateDirectory(baselineDirectory);
                string runtimePath = Path.Combine(SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target),
                    entrySettings.RuntimeAssemblyName + ".dll");
                if (hybrid)
                {
                    manifest.Assemblies[0] = CopyCode(runtimePath, entrySettings.RuntimeAssemblyName, string.Empty);
                }

                // 只收集明确配置的元数据，并归档本次主包的全部 AOT 以校验最终构建。
                string strippedDirectory = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
                string[] metadataNames = HybridCLRSettings.Instance.patchAOTAssemblies;
                for (int index = 0; index < manifest.Metadata.Length; index++)
                {
                    string name = metadataNames[index];
                    manifest.Metadata[index] = CopyCode(
                        Path.Combine(strippedDirectory, name + ".dll"), name, "AOTMetadata");
                }
                foreach (string file in Directory.GetFiles(strippedDirectory, "*.dll"))
                {
                    File.Copy(file, Path.Combine(baselineDirectory, Path.GetFileName(file)), false);
                }
                File.WriteAllText(Path.Combine(baselineDirectory, "configuration.txt"), GetConfiguration(target));
                File.WriteAllText(Path.Combine(CODEROOT, "manifest.json"), JsonUtility.ToJson(manifest, true));

                // 身份嵌入 AOT 主包，不从热更新业务配置读取。
                Directory.CreateDirectory("Assets/Resources");
                File.WriteAllText(IDENTITYPATH, JsonUtility.ToJson(new HotUpdatePlayerIdentity
                {
                    Platform = target.ToString(),
                    BaselineID = baselineID
                }, true));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (hybrid)
                {
                    Debug.Log($"[Build][代码子步骤开始] 构建并复制内置 CodePackage，版本={manifest.ReleaseID}");
                    BuildCodePackage(target, manifest.ReleaseID);
                    Debug.Log("[Build][代码子步骤完成] CodePackage 构建及内置复制完成。");
                }
                File.WriteAllText(Path.Combine(baselineDirectory, "code-manifest.json"),
                    JsonUtility.ToJson(manifest, true));
                File.WriteAllText(Path.Combine(baselineDirectory, "aot-input.txt"),
                    GameBuildFingerprint.SaveCodeSnapshot(baselineDirectory, !hybrid));
                File.WriteAllText(Path.Combine(baselineDirectory, "code-input.txt"),
                    GameBuildFingerprint.SaveCodeSnapshot(baselineDirectory, true));
                File.WriteAllText(Path.Combine(baselineDirectory, "generated-cpp.txt"),
                    GameBuildFingerprint.DirectoryHash(SettingsUtil.GeneratedCppDir));
                Debug.Log($"[HybridCLR] 主包代码准备完成：{target}，基线={baselineID}。" +
                    "本操作仅完成主包代码与 AOT 基线准备；" +
                    "完整安装包请执行 ★AlloyFramework★/出包/一键完整出包。");
            }
            catch
            {
                // 失败不会留下可供最终 Player 校验使用的已确认基线。
                Directory.CreateDirectory(baselineDirectory);
                File.WriteAllText(Path.Combine(baselineDirectory, "failed.txt"), "主包代码准备失败，请重新准备。");
                throw;
            }
            finally
            {
                m_isPreparing = false;
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// 在最终 Player 构建前检查主包代码、基线及目标平台是否匹配。
        /// </summary>
        /// <param name="report">当前构建报告。</param>
        /// <exception cref="BuildFailedException">主包代码尚未准备或配置已变化。</exception>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (m_isPreparing)
            {
                return;
            }
            HotUpdatePlayerIdentity identity = ReadIdentity(report.summary.platform);
            string directory = Path.Combine(BASELINEROOT, identity.Platform, identity.BaselineID);
            if (!File.Exists(Path.Combine(directory, "code-manifest.json")) ||
                File.Exists(Path.Combine(directory, "failed.txt")) ||
                File.ReadAllText(Path.Combine(directory, "configuration.txt")) !=
                    GetConfiguration(report.summary.platform))
            {
                throw new BuildFailedException("主包准备结果缺失或配置已改变，请重新生成主包代码与 AOT 基线。");
            }
            // 在长时间 IL2CPP 构建前拦截源码变化，旧基线必须重新准备。
            string fingerprintPath = Path.Combine(directory, "code-input.txt");
            if (!File.Exists(fingerprintPath) || File.ReadAllText(fingerprintPath) != GameBuildFingerprint.Code(true))
            {
                string changes = GameBuildFingerprint.DescribeCodeChanges(directory, true);
                throw new BuildFailedException("主包代码或 AOT 输入已变化，请重新生成主包代码与 AOT 基线" +
                    "或执行一键完整出包。\n" +
                    changes);
            }
            string generatedPath = Path.Combine(directory, "generated-cpp.txt");
            if (!File.Exists(generatedPath) || File.ReadAllText(generatedPath) !=
                GameBuildFingerprint.DirectoryHash(SettingsUtil.GeneratedCppDir))
            {
                throw new BuildFailedException("HybridCLR 生成的桥接代码已变化，请重新准备当前平台主包。");
            }
        }

        /// <summary>
        /// 比较最终 Player 与准备阶段的全部裁剪 AOT，确认可复用的主包基线。
        /// </summary>
        /// <param name="report">最终 Player 构建报告。</param>
        /// <exception cref="BuildFailedException">最终 AOT 与准备结果不一致。</exception>
        public void OnPostprocessBuild(BuildReport report)
        {
            if (m_isPreparing || report.summary.result == UnityEditor.Build.Reporting.BuildResult.Failed ||
                report.summary.result == UnityEditor.Build.Reporting.BuildResult.Cancelled)
            {
                return;
            }
            HotUpdatePlayerIdentity identity = ReadIdentity(report.summary.platform);
            string directory = Path.Combine(BASELINEROOT, identity.Platform, identity.BaselineID);
            string finalDirectory = SettingsUtil.GetAssembliesPostIl2CppStripDir(report.summary.platform);
            string[] archivedFiles = Directory.GetFiles(directory, "*.dll");
            // 比较名称集合并报告具体差异，避免只凭数量给出无法定位的错误。
            var archivedNames = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string file in archivedFiles)
            {
                archivedNames.Add(Path.GetFileName(file));
            }
            var finalNames = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(finalDirectory, "*.dll"))
            {
                finalNames.Add(Path.GetFileName(file));
            }
            if (!archivedNames.SetEquals(finalNames))
            {
                var addedNames = new SortedSet<string>(finalNames, StringComparer.Ordinal);
                addedNames.ExceptWith(archivedNames);
                var removedNames = new SortedSet<string>(archivedNames, StringComparer.Ordinal);
                removedNames.ExceptWith(finalNames);
                throw new BuildFailedException(
                    $"最终 AOT 程序集集合改变，不能确认本次主包基线 {identity.BaselineID}。" +
                    $"新增：[{string.Join(", ", addedNames)}]；缺失：[{string.Join(", ", removedNames)}]。" +
                    "请执行 ★AlloyFramework★/出包/单项操作/单独生成主包代码与 AOT 基线，再重新构建 Player。");
            }
            foreach (string file in archivedFiles)
            {
                string finalFile = Path.Combine(finalDirectory, Path.GetFileName(file));
                if (!File.Exists(finalFile) || GetHash(file) != GetHash(finalFile))
                {
                    throw new BuildFailedException($"最终 AOT 与准备阶段不一致：{Path.GetFileName(file)}。请重新准备主包。");
                }
            }
            File.WriteAllText(Path.Combine(directory, "confirmed.txt"), report.summary.outputPath);
            Debug.Log($"[HybridCLR] 最终 Player AOT 校验通过，主包基线已确认：{identity.BaselineID}");
        }

        internal static HotUpdateCodeFile CopyCode(string source, string assemblyName, string subdirectory)
        {
            if (!File.Exists(source))
            {
                throw new BuildFailedException($"缺少目标平台 DLL：{source}。请检查 HybridCLR 配置及生成日志。");
            }
            string destination = Path.Combine(CODEROOT, subdirectory, assemblyName + ".dll.bytes");
            File.Copy(source, destination, true);
            string prefix = string.IsNullOrEmpty(subdirectory) ? string.Empty : subdirectory + "/";
            return new HotUpdateCodeFile
            {
                AssemblyName = assemblyName,
                Location = "HotUpdateCode/" + prefix + assemblyName + ".dll",
                SHA256 = GetHash(source)
            };
        }

        private static void UpdateMetadataList()
        {
            // 从官方泛型分析结果追加实际需要的元数据，不无差别加载裁剪目录中的 DLL。
            string referencePath = Path.Combine(
                Application.dataPath, HybridCLRSettings.Instance.outputAOTGenericReferenceFile);
            string referenceSource = File.ReadAllText(referencePath);
            Match list = Regex.Match(referenceSource, @"PatchedAOTAssemblyList[\s\S]*?\{([\s\S]*?)\};");
            if (!list.Success)
            {
                throw new BuildFailedException("无法读取 HybridCLR 8.14.1 的 AOT 泛型分析结果。");
            }
            var names = new SortedSet<string>(
                HybridCLRSettings.Instance.patchAOTAssemblies ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (Match file in Regex.Matches(list.Groups[1].Value, "\"([^\"]+)\\.dll\""))
            {
                names.Add(file.Groups[1].Value);
            }
            string[] metadataNames = new string[names.Count];
            names.CopyTo(metadataNames);
            HybridCLRSettings.Instance.patchAOTAssemblies = metadataNames;
            HybridCLRSettings.Save();
        }

        private static void BuildCodePackage(BuildTarget target, string version)
        {
            var parameters = new RawFileBuildParameters
            {
                BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot(),
                BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot(),
                BuildPipeline = EBuildPipeline.RawFileBuildPipeline.ToString(),
                BuildBundleType = (int)EBuildBundleType.RawBundle,
                BuildTarget = target,
                PackageName = "CodePackage",
                PackageVersion = version,
                BuildinFileCopyOption = EBuildinFileCopyOption.ClearAndCopyAll,
                BuildinFileCopyParams = string.Empty,
                FileNameStyle = EFileNameStyle.HashName,
                IncludePathInHash = true
            };
            YooAsset.Editor.BuildResult result = new RawFileBuildPipeline().Run(parameters, true);
            if (!result.Success)
            {
                throw new BuildFailedException(result.ErrorInfo);
            }
        }

        internal static HotUpdatePlayerIdentity ReadIdentity(BuildTarget target)
        {
            ValidateTarget(target);
            if (!File.Exists(IDENTITYPATH))
            {
                throw new BuildFailedException(
                    "请先执行 ★AlloyFramework★/出包/单项操作/单独生成主包代码与 AOT 基线。");
            }
            var identity = JsonUtility.FromJson<HotUpdatePlayerIdentity>(File.ReadAllText(IDENTITYPATH));
            if (identity == null || identity.Platform != target.ToString() ||
                string.IsNullOrEmpty(identity.BaselineID) ||
                identity.BaselineID.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new BuildFailedException("当前平台与主包代码身份不匹配，请重新准备。");
            }
            return identity;
        }

        internal static string GetConfiguration(BuildTarget target)
        {
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            GameHotUpdateSettings entrySettings = GameHotUpdateSettings.Load();
            return string.Join("\n", Application.unityVersion, HybridCLRSettings.Instance.enable,
                new InstallerController().PackageVersion, target,
                PlayerSettings.GetScriptingDefineSymbolsForGroup(group),
                PlayerSettings.GetManagedStrippingLevel(group), PlayerSettings.GetScriptingBackend(group),
                PlayerSettings.GetApiCompatibilityLevel(group), PlayerSettings.Android.targetArchitectures,
                EditorUserBuildSettings.development,
                string.Join(",", HybridCLRSettings.Instance.patchAOTAssemblies),
                entrySettings.RuntimeAssemblyName, entrySettings.InstallerTypeName);
        }

        private static void ValidateEntry(GameHotUpdateSettings settings)
        {
            // 构建前检查配置指向实际业务代码，避免把错误入口写入发布清单。
            Assembly runtimeAssembly = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == settings.RuntimeAssemblyName)
                {
                    runtimeAssembly = assembly;
                    break;
                }
            }
            if (runtimeAssembly == null)
            {
                throw new BuildFailedException($"入口配置的程序集未加载：{settings.RuntimeAssemblyName}");
            }
            int entryCount = 0;
            foreach (Type type in runtimeAssembly.GetTypes())
            {
                if (!type.IsAbstract && !type.IsGenericTypeDefinition && typeof(IGameEntry).IsAssignableFrom(type))
                {
                    entryCount++;
                }
            }
            Type installerType = runtimeAssembly.GetType(settings.InstallerTypeName, false);
            MethodInfo method = installerType?.GetMethod(
                "Install", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (entryCount != 1 || method == null || method.ReturnType != typeof(void) ||
                method.ContainsGenericParameters)
            {
                throw new BuildFailedException(
                    "入口配置的程序集必须包含唯一 IGameEntry，安装类型必须提供 public static void Install()。");
            }
        }

        private static string GetHash(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static void CheckEditorState()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || m_isPreparing)
            {
                throw new BuildFailedException("请等待编译完成并退出运行模式，不能重复执行代码准备。");
            }
        }

        private static void ValidateTarget(BuildTarget target)
        {
            if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.Android)
            {
                throw new BuildFailedException("请先在 Build Settings 切换到 Windows x64 或 Android，并等待编译完成。");
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
            {
                throw new BuildFailedException($"缺少 {target} 平台模块，请通过 Unity Hub 安装。");
            }
        }
    }
}
