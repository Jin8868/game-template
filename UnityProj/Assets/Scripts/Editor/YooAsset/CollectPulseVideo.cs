using System;
using System.IO;
using YooAsset.Editor;

namespace Game.Editor
{
    /// <summary>只收集可交给 Pulse 播放器的原始视频，排除说明和离线音轨。</summary>
    [DisplayName("收集视频: Pulse原始文件")]
    public sealed class CollectPulseVideo : IFilterRule
    {
        /// <summary>按文件类型搜索原始资源。</summary>
        public string FindAssetType => EAssetSearchType.All.ToString();

        /// <summary>判断文件是否为 Pulse 视频。</summary>
        /// <param name="data">YooAssets 提供的采集上下文。</param>
        /// <returns>扩展名为 .pulse 时返回 true。</returns>
        public bool IsCollectAsset(FilterRuleData data)
        {
            return string.Equals(Path.GetExtension(data.AssetPath), ".pulse", StringComparison.OrdinalIgnoreCase);
        }
    }
}
