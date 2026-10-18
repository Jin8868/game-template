using System;
using System.Collections.Generic;
using System.IO;
using AlloyFramework.HybridCLR;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.HotUpdate;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using YooAsset.Editor;
using BuildReport = UnityEditor.Build.Reporting.BuildReport;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Game.Editor
{
    /// <summary>完整构建主包、增量生成补丁并发布到本机目录。</summary>
    public static class GameBuildPipeline
    {
        private const string BASELINEROOT = "BuildArtifacts/AOTBaselines"; // 主包 AOT 基线归档目录。
        private const string CODEROOT = "Assets/Res/HotUpdateCode"; // 热更新代码资源目录。
        private static bool m_running; // 防止同一编辑器并发执行构建。
        private static string m_currentStep; // 当前执行步骤，用于定位失败阶段。
        private static Stopwatch m_stepTimer; // 当前步骤的实际耗时。

        [Serializable]
        private sealed class BuildStepRecord
        {
            /// <summary>步骤输入的内容指纹。</summary>
            public string Input;
            /// <summary>步骤输出目录。</summary>
            public string Directory;
            /// <summary>资源包版本。</summary>
            public string Version;
            /// <summary>对应的主包基线。</summary>
            public string BaselineID;
        }

        [MenuItem("★AlloyFramework★/出包/一键完整出包")]
        private static void BuildMainPackage()
        {
            Run(false);
        }

        [MenuItem("★AlloyFramework★/出包/一键生成热更新补丁")]
        private static void BuildPatch()
        {
            Run(true);
        }

        private static void Run(bool patch)
        {
            if (m_running || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                throw new BuildFailedException("请退出运行模式并等待脚本编译完成，不能并发构建。");
            }
            m_running = true;
            var buildTimer = Stopwatch.StartNew();
            BeginStep("构建输入检查", patch ? "热更新补丁" : "完整主包");
            try
            {
                AssetDatabase.SaveAssets();
                BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
                if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.Android)
                {
                    throw new BuildFailedException("一键出包只支持 Windows x64 和 Android。");
                }
                if (target == BuildTarget.Android && EditorUserBuildSettings.exportAsGoogleAndroidProject)
                {
                    throw new BuildFailedException("一键出包需要关闭 Export Project，直接生成 APK 或 AAB。");
                }
                GameHotUpdateSettings settings = GameHotUpdateSettings.Load();
                EndStep("完成", $"平台={target}，代码模式={settings.CodeMode}，资源模式={settings.ResourceMode}");
                if (patch)
                {
                    GeneratePatch(target, settings);
                }
                else
                {
                    GenerateMain(target, settings);
                }
                Debug.Log($"[Build][全部完成] 总耗时={buildTimer.Elapsed.TotalSeconds:F1}秒");
            }
            catch (Exception exception)
            {
                EndStep("失败", exception.Message);
                Debug.LogError($"[Build][构建失败] 总耗时={buildTimer.Elapsed.TotalSeconds:F1}秒");
                Debug.LogException(exception);
                throw;
            }
            finally
            {
                m_running = false;
                m_currentStep = null;
                m_stepTimer = null;
                EditorUtility.ClearProgressBar();
            }
        }

        private static void GenerateMain(BuildTarget target, GameHotUpdateSettings settings)
        {
            // 完整出包始终重新准备代码和基线，不读取旧主包的构建缓存。
            BeginStep("1/5 主包代码和 AOT 基线", "重新生成主包代码资源和 AOT 基线");
            GameHotUpdatePreparation.Configure();
            GameHotUpdatePreparation.PrepareMainPackage();
            HotUpdatePlayerIdentity identity = GameHotUpdatePreparation.ReadIdentity(target);
            var packages = new List<BuildStepRecord>();
            if (settings.CodeMode == EGameCodeMode.HybridCLR)
            {
                packages.Add(new BuildStepRecord
                {
                    Directory = GetPackageDirectory("CodePackage", identity.BaselineID),
                    Version = identity.BaselineID,
                    BaselineID = identity.BaselineID
                });
            }
            EndStep("完成", $"基线={identity.BaselineID}");

            // 重新构建全部资源包并复制内置资源，不计算用于判断复用的输入及产物指纹。
            packages.Add(BuildPackage("AudioPackage", true, "2/5"));
            packages.Add(BuildPackage("VideoPackage", true, "3/5"));
            packages.Add(BuildPackage("DefaultPackage", true, "4/5"));

            BeginStep("5/5 Unity Player", "重新构建完整安装包");
            string playerDirectory = Path.Combine("BuildArtifacts/Players", target.ToString(),
                settings.CodeMode.ToString());
            string playerPath = Path.Combine(playerDirectory, target == BuildTarget.Android ?
                "game-template" + (EditorUserBuildSettings.buildAppBundle ? ".aab" : ".apk") : "game-template.exe");
            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    scenes.Add(scene.path);
                }
            }
            if (scenes.Count == 0)
            {
                throw new BuildFailedException("Build Settings 没有启用的启动场景。");
            }

            // 始终调用 Unity 构建流程，最终 AOT 校验仍由构建回调确认新基线。
            Directory.CreateDirectory(playerDirectory);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                target = target,
                locationPathName = playerPath,
                options = GetPlayerOptions()
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Player 构建失败：{report.summary.result}");
            }
            EndStep("完成", $"输出={Path.GetFullPath(playerPath)}");

            // 新主包成功后发布本次全部资源，不再比较旧发布记录。
            BeginStep("发布主包资源", "复制本次构建资源并更新服务器版本指针");
            string published = Publish(identity.BaselineID, identity.BaselineID, packages);
            EndStep("完成", $"版本={identity.BaselineID}，输出={Path.GetFullPath(published)}");
            Debug.Log($"[Build] 一键完整出包完成：{Path.GetFullPath(playerPath)}");
            EditorUtility.RevealInFinder(Path.GetFullPath(playerPath));
        }

        private static void GeneratePatch(BuildTarget target, GameHotUpdateSettings settings)
        {
            BeginStep("补丁基线检查", "检查旧主包的 AOT 输入与已确认基线");
            HotUpdatePlayerIdentity identity = GameHotUpdatePreparation.ReadIdentity(target);
            string baseline = Path.Combine(BASELINEROOT, target.ToString(), identity.BaselineID);
            bool hybrid = settings.CodeMode == EGameCodeMode.HybridCLR;
            string aotFingerprint = Path.Combine(baseline, "aot-input.txt");
            if (!File.Exists(Path.Combine(baseline, "confirmed.txt")) ||
                !File.Exists(aotFingerprint) ||
                File.ReadAllText(aotFingerprint) != GameBuildFingerprint.Code(!hybrid) ||
                File.ReadAllText(Path.Combine(baseline, "configuration.txt")) !=
                GameHotUpdatePreparation.GetConfiguration(target))
            {
                string changes = GameBuildFingerprint.DescribeCodeChanges(baseline, !hybrid);
                throw new BuildFailedException(
                    "AOT 输入已变化或基线尚未确认。请先一键完整出包，不能向旧主包发布此补丁。\n" + changes);
            }
            string releaseID = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            EndStep("完成", $"基线={identity.BaselineID}");
            var packages = new List<BuildStepRecord>();
            BeginStep("1/4 热更新代码", hybrid ? "检查 DLL 编译缓存及旧主包 API 兼容性" : "Default 模式");
            if (hybrid)
            {
                // 只编译目标平台代码，禁止 GenerateAll 或重新生成旧基线的 AOT 元数据。
                string compileInput = GameBuildFingerprint.Code(true) + identity.BaselineID;
                BuildStepRecord compiled = LoadRecord("patch-compiled");
                string compileReason = GetRebuildReason(compiled, compileInput, null);
                if (compileReason != null)
                {
                    Debug.Log($"[Build][重新生成] 热更新 DLL：{compileReason}");
                    CompileDllCommand.CompileDll(target, EditorUserBuildSettings.development);
                    string compiledDirectory = Path.Combine("BuildArtifacts/PatchCompiled", target.ToString(),
                        identity.BaselineID);
                    Directory.CreateDirectory(compiledDirectory);
                    File.Copy(Path.Combine(SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target),
                        settings.RuntimeAssemblyName + ".dll"),
                        Path.Combine(compiledDirectory, settings.RuntimeAssemblyName + ".dll"), true);
                    compiled = SaveRecord("patch-compiled", compileInput, compiledDirectory, null,
                        identity.BaselineID, null);
                }
                else
                {
                    Debug.Log($"[Build][跳过 DLL 编译] 输入及产物未变化：{Path.GetFullPath(compiled.Directory)}");
                }
                string runtimeDLL = Path.Combine(compiled.Directory, settings.RuntimeAssemblyName + ".dll");
                Debug.Log("[Build][代码子步骤] 检查热更新 DLL 引用的旧主包 AOT API。");
                var checker = new MissingMetadataChecker(baseline, new[] { settings.RuntimeAssemblyName });
                if (!checker.Check(runtimeDLL))
                {
                    throw new BuildFailedException("补丁引用了旧主包中不存在的 AOT API，请查看检查日志并重新构建主包。");
                }
                var manifest = JsonUtility.FromJson<HotUpdateCodeManifest>(
                    File.ReadAllText(Path.Combine(baseline, "code-manifest.json")));
                manifest.ReleaseID = releaseID;
                manifest.Assemblies[0] = GameHotUpdatePreparation.CopyCode(
                    runtimeDLL, settings.RuntimeAssemblyName, string.Empty);
                foreach (HotUpdateCodeFile metadata in manifest.Metadata)
                {
                    string original = Path.Combine(baseline, metadata.AssemblyName + ".dll");
                    if (GameBuildFingerprint.FileHash(original) != metadata.SHA256)
                    {
                        throw new BuildFailedException($"已归档元数据内容发生变化：{metadata.AssemblyName}");
                    }
                    GameHotUpdatePreparation.CopyCode(original, metadata.AssemblyName, "AOTMetadata");
                }
                File.WriteAllText(Path.Combine(CODEROOT, "manifest.json"), JsonUtility.ToJson(manifest, true));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("[Build][代码子步骤] 构建 CodePackage，复用旧基线的 AOT 元数据。");
                string codeDirectory = GamePackageBuildService.Build("CodePackage", releaseID, false);
                packages.Add(SaveRecord("patch-code", compileInput, codeDirectory, releaseID,
                    identity.BaselineID, null));
                EndStep("完成", $"输出={Path.GetFullPath(codeDirectory)}");
            }
            else
            {
                EndStep("跳过", "Default 模式只发布资源补丁");
            }
            BuildStepRecord audio = BuildPackage("AudioPackage", false, "2/4");
            BuildStepRecord video = BuildPackage("VideoPackage", false, "3/4");
            BuildStepRecord resources = BuildPackage("DefaultPackage", false, "4/4");
            packages.Add(audio);
            packages.Add(video);
            packages.Add(resources);
            BeginStep("发布热更新补丁", "复制发布文件并更新服务器版本指针");
            string published = Publish(identity.BaselineID, releaseID, packages);
            EndStep("完成", $"版本={releaseID}，输出={Path.GetFullPath(published)}");
            Debug.Log($"[HotUpdate] 补丁发布完成：{releaseID}。请重启同一个主包验证，未构建 Player。");
        }

        private static BuildStepRecord BuildPackage(string packageName, bool buildin, string phase)
        {
            // 外层不再读取资源内容做指纹校验，直接使用 YooAsset 自身的构建流程。
            BeginStep($"{phase} {packageName}", buildin ? "构建资源包并复制内置资源" : "构建补丁资源包");
            string version = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            string directory = GamePackageBuildService.Build(packageName, version, buildin);
            EndStep("完成", $"版本={version}，输出={Path.GetFullPath(directory)}");
            return new BuildStepRecord { Directory = directory, Version = version };
        }

        private static void BeginStep(string step, string detail)
        {
            m_currentStep = step;
            m_stepTimer = Stopwatch.StartNew();
            Debug.Log($"[Build][开始] {step} | {detail}");
        }

        private static void EndStep(string status, string detail)
        {
            double seconds = m_stepTimer == null ? 0 : m_stepTimer.Elapsed.TotalSeconds;
            string message = $"[Build][{status}] {m_currentStep} | 耗时={seconds:F1}秒 | {detail}";
            if (status == "失败")
            {
                Debug.LogError(message);
            }
            else
            {
                Debug.Log(message);
            }
            m_stepTimer?.Stop();
        }

        private static string Publish(string baselineID, string releaseID, List<BuildStepRecord> packages)
        {
            GameHotUpdateSettings settings = GameHotUpdateSettings.Load();
            string root = Path.Combine("BuildArtifacts/LocalCDN", EditorUserBuildSettings.activeBuildTarget.ToString(),
                settings.Channel, baselineID);
            string destination = Path.Combine(root, "releases", releaseID);
            var manifest = new GameReleaseManifest
            {
                Platform = EditorUserBuildSettings.activeBuildTarget.ToString(),
                BaselineID = baselineID,
                ReleaseID = releaseID,
                Packages = new GameReleasePackage[packages.Count]
            };
            for (int index = 0; index < packages.Count; index++)
            {
                BuildStepRecord package = packages[index];
                string packageName = new DirectoryInfo(package.Directory).Parent.Name;
                manifest.Packages[index] = new GameReleasePackage { Name = packageName, Version = package.Version };
            }
            string json = JsonUtility.ToJson(manifest, true);
            if (Directory.Exists(destination))
            {
                // 已有版本目录必须完全匹配，不能覆盖旧发布。
                if (!File.Exists(Path.Combine(destination, "release-manifest.json")) ||
                    File.ReadAllText(Path.Combine(destination, "release-manifest.json")) != json)
                {
                    throw new BuildFailedException("发布目录已存在且内容不同，拒绝覆盖不可变版本。");
                }
                for (int index = 0; index < packages.Count; index++)
                {
                    VerifyCopy(packages[index].Directory, Path.Combine(destination, manifest.Packages[index].Name));
                }
            }
            else
            {
                string staging = Path.Combine(root, "staging", releaseID);
                if (Directory.Exists(staging))
                {
                    throw new BuildFailedException("存在同名未完成发布，请检查 staging 目录。");
                }
                Directory.CreateDirectory(staging);
                for (int index = 0; index < packages.Count; index++)
                {
                    string copy = Path.Combine(staging, manifest.Packages[index].Name);
                    CopyDirectory(packages[index].Directory, copy);
                    VerifyCopy(packages[index].Directory, copy);
                }
                File.WriteAllText(Path.Combine(staging, "release-manifest.json"), json);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                Directory.Move(staging, destination);
            }
            // 完整发布目录提交成功后，原子替换小指针；启动时只读取一次该指针。
            string pointer = Path.Combine(root, "release.json");
            string pending = pointer + ".pending";
            File.WriteAllText(pending, json);
            if (File.Exists(pointer))
            {
                File.Replace(pending, pointer, null);
            }
            else
            {
                File.Move(pending, pointer);
            }
            return destination;
        }

        private static bool IsCurrentRelease(string baselineID, string releaseID)
        {
            GameHotUpdateSettings settings = GameHotUpdateSettings.Load();
            string path = Path.Combine("BuildArtifacts/LocalCDN", EditorUserBuildSettings.activeBuildTarget.ToString(),
                settings.Channel, baselineID, "release.json");
            return File.Exists(path) &&
                JsonUtility.FromJson<GameReleaseManifest>(File.ReadAllText(path)).ReleaseID == releaseID;
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.TrimEnd('/', '\\').Length).TrimStart('/', '\\');
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, false);
            }
        }

        private static void VerifyCopy(string source, string destination)
        {
            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            if (!Directory.Exists(destination) || files.Length !=
                Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Length)
            {
                throw new BuildFailedException("发布文件集合不完整，保留原发布指针。");
            }
            // 检查复制后的文件集合和长度，避免再次完整读取大视频计算哈希。
            foreach (string file in files)
            {
                string relative = file.Substring(source.TrimEnd('/', '\\').Length).TrimStart('/', '\\');
                string copy = Path.Combine(destination, relative);
                if (!File.Exists(copy) || new FileInfo(file).Length != new FileInfo(copy).Length)
                {
                    throw new BuildFailedException($"发布文件校验失败：{relative}");
                }
            }
        }

        private static bool IsReusable(BuildStepRecord record, string input, string buildinPackage)
        {
            return GetRebuildReason(record, input, buildinPackage) == null;
        }

        private static string GetRebuildReason(BuildStepRecord record, string input, string buildinPackage)
        {
            // 代码编译缓存保留输入比较和目录检查，不再读取产物内容计算哈希。
            if (record == null)
            {
                return "没有成功缓存记录";
            }
            if (record.Input != input)
            {
                return "输入指纹变化（源码、配置、依赖或指纹算法版本）";
            }
            if (!Directory.Exists(record.Directory))
            {
                return "构建产物目录缺失";
            }
            if (!File.Exists(Path.Combine(record.Directory, GameHotUpdateSettings.Load().RuntimeAssemblyName + ".dll")))
            {
                return "缓存的业务 DLL 缺失";
            }
            if (buildinPackage != null)
            {
                if (!File.Exists(Path.Combine(GetBuildin(buildinPackage), "BuildinCatalog.bytes")))
                {
                    return "内置资源目录缺少 BuildinCatalog.bytes";
                }
            }
            return null;
        }

        private static BuildStepRecord SaveRecord(string key, string input, string directory, string version,
            string baselineID, string buildinPackage)
        {
            if (!Directory.Exists(directory) ||
                Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length == 0)
            {
                throw new BuildFailedException($"构建产物缺失，不能缓存成功结果：{directory}");
            }
            if (buildinPackage != null &&
                !File.Exists(Path.Combine(GetBuildin(buildinPackage), "BuildinCatalog.bytes")))
            {
                throw new BuildFailedException($"内置资源目录缺少 {buildinPackage}/BuildinCatalog.bytes。");
            }
            var record = new BuildStepRecord
            {
                Input = input,
                Directory = directory,
                Version = version,
                BaselineID = baselineID
            };
            string path = GetRecordPath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(record, true));
            return record;
        }

        private static BuildStepRecord LoadRecord(string key)
        {
            string path = GetRecordPath(key);
            return File.Exists(path) ? JsonUtility.FromJson<BuildStepRecord>(File.ReadAllText(path)) : null;
        }

        private static string GetRecordPath(string key)
        {
            return Path.Combine("BuildArtifacts/BuildCache", EditorUserBuildSettings.activeBuildTarget.ToString(),
                GameHotUpdateSettings.Load().CodeMode.ToString(), key + ".json");
        }

        private static string GetPackageDirectory(string packageName, string version)
        {
            return Path.Combine(AssetBundleBuilderHelper.GetDefaultBuildOutputRoot(),
                EditorUserBuildSettings.activeBuildTarget.ToString(), packageName, version);
        }

        private static string GetBuildin(string packageName)
        {
            return Path.Combine(AssetBundleBuilderHelper.GetStreamingAssetsRoot(), packageName);
        }

        private static BuildOptions GetPlayerOptions()
        {
            BuildOptions options = BuildOptions.None;
            if (EditorUserBuildSettings.development)
            {
                options |= BuildOptions.Development;
            }
            if (EditorUserBuildSettings.allowDebugging && EditorUserBuildSettings.development)
            {
                options |= BuildOptions.AllowDebugging;
            }
            if (EditorUserBuildSettings.connectProfiler)
            {
                options |= BuildOptions.ConnectWithProfiler;
            }
            return options;
        }
    }
}
