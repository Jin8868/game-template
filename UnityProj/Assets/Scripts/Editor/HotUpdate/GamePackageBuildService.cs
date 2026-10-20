using System;
using UnityEditor;
using YooAsset;
using YooAsset.Editor;

namespace Game.Editor
{
    internal static class GamePackageBuildService
    {
        internal static string Build(string packageName, string version, bool copyBuildin)
        {
            // 在 Collector 验证和实际构建前准备视频长音轨，普通资源包不触发媒体转换。
            if (packageName == "AudioPackage") GameVideoAudioPackageBuilder.Prepare();
            AssetBundleCollectorSettingData.Setting.CheckPackageConfigError(packageName);
            string pipelineName = GetPipeline(packageName);
            BuildParameters parameters;
            if (packageName == "DefaultPackage")
            {
                parameters = new ScriptableBuildParameters
                {
                    EnableSharePackRule = true,
                    VerifyBuildingResult = true,
                    FileNameStyle = AssetBundleBuilderSetting.GetPackageFileNameStyle(packageName, pipelineName),
                    CompressOption = AssetBundleBuilderSetting.GetPackageCompressOption(packageName, pipelineName),
                    ClearBuildCacheFiles = AssetBundleBuilderSetting.GetPackageClearBuildCache(
                        packageName, pipelineName),
                    UseAssetDependencyDB = AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(
                        packageName, pipelineName),
                    EncryptionServices = CreateService<IEncryptionServices>(
                        AssetBundleBuilderSetting.GetPackageEncyptionServicesClassName(packageName, pipelineName)),
                    ManifestProcessServices = CreateService<IManifestProcessServices>(
                        AssetBundleBuilderSetting.GetPackageManifestProcessServicesClassName(
                            packageName, pipelineName)),
                    ManifestRestoreServices = CreateService<IManifestRestoreServices>(
                        AssetBundleBuilderSetting.GetPackageManifestRestoreServicesClassName(
                            packageName, pipelineName)),
                    BuiltinShadersBundleName = DefaultPackRule.CreateShadersPackRuleResult().GetBundleName(
                        packageName, AssetBundleCollectorSettingData.Setting.UniqueBundleName)
                };
            }
            else
            {
                parameters = new RawFileBuildParameters
                {
                    FileNameStyle = EFileNameStyle.HashName,
                    IncludePathInHash = true
                };
            }
            // 主包明确复制全部内置文件；补丁构建不改动主包 StreamingAssets。
            parameters.BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
            parameters.BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            parameters.BuildPipeline = pipelineName;
            parameters.BuildBundleType = (int)(packageName == "DefaultPackage" ?
                EBuildBundleType.AssetBundle : EBuildBundleType.RawBundle);
            parameters.BuildTarget = EditorUserBuildSettings.activeBuildTarget;
            parameters.PackageName = packageName;
            parameters.PackageVersion = version;
            parameters.BuildinFileCopyOption = copyBuildin ? EBuildinFileCopyOption.ClearAndCopyAll :
                EBuildinFileCopyOption.None;
            parameters.BuildinFileCopyParams = string.Empty;
            YooAsset.Editor.BuildResult result = packageName == "DefaultPackage" ?
                new ScriptableBuildPipeline().Run(parameters, true) : new RawFileBuildPipeline().Run(parameters, true);
            if (!result.Success)
            {
                throw new UnityEditor.Build.BuildFailedException($"{packageName} 构建失败：{result.ErrorInfo}");
            }
            return result.OutputPackageDirectory;
        }

        internal static string GetOptions(string packageName)
        {
            string pipeline = GetPipeline(packageName);
            return string.Join("|", pipeline,
                AssetBundleBuilderSetting.GetPackageFileNameStyle(packageName, pipeline),
                AssetBundleBuilderSetting.GetPackageCompressOption(packageName, pipeline),
                AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipeline),
                AssetBundleBuilderSetting.GetPackageClearBuildCache(packageName, pipeline),
                AssetBundleBuilderSetting.GetPackageEncyptionServicesClassName(packageName, pipeline),
                AssetBundleBuilderSetting.GetPackageManifestProcessServicesClassName(packageName, pipeline),
                AssetBundleBuilderSetting.GetPackageManifestRestoreServicesClassName(packageName, pipeline));
        }

        private static string GetPipeline(string packageName)
        {
            return (packageName == "DefaultPackage" ? EBuildPipeline.ScriptableBuildPipeline :
                EBuildPipeline.RawFileBuildPipeline).ToString();
        }

        private static T CreateService<T>(string className) where T : class
        {
            if (string.IsNullOrEmpty(className))
            {
                return null;
            }
            foreach (Type type in EditorTools.GetAssignableTypes(typeof(T)))
            {
                if (type.FullName == className)
                {
                    return Activator.CreateInstance(type) as T;
                }
            }
            throw new UnityEditor.Build.BuildFailedException($"无法创建资源构建服务：{className}");
        }
    }
}
