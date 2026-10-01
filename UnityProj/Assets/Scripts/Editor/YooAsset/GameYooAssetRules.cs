using System;
using System.IO;
using UnityEditor;
using YooAsset.Editor;

namespace Game.Editor
{
    [DisplayName("定位地址: Res相对路径")]
    public sealed class AddressByResPath : IAddressRule
    {
        private const string ResRoot = "Assets/Res/";

        public string GetAssetAddress(AddressRuleData data)
        {
            if (!data.AssetPath.StartsWith(ResRoot, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Asset is outside the Res root: {data.AssetPath}");
            }

            var relativePath = data.AssetPath.Substring(ResRoot.Length);
            return Path.ChangeExtension(relativePath, null).Replace('\\', '/');
        }
    }

    [DisplayName("收集场景: 排除Build Settings场景")]
    public sealed class CollectHotUpdateScene : IFilterRule
    {
        public string FindAssetType => EAssetSearchType.Scene.ToString();

        public bool IsCollectAsset(FilterRuleData data)
        {
            var extension = Path.GetExtension(data.AssetPath);
            if (!string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".scene", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var assetPath = data.AssetPath.Replace('\\', '/');
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled &&
                    string.Equals(scene.path.Replace('\\', '/'), assetPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
