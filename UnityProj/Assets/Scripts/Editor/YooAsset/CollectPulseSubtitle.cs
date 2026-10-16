using System;
using System.IO;
using YooAsset.Editor;

namespace Game.Editor
{
    /// <summary>只采集 SRT TextAsset，避免把视频或离线音轨打进普通资源包。</summary>
    [DisplayName("收集字幕: SRT文本")]
    public sealed class CollectPulseSubtitle : IFilterRule
    {
        /// <summary>字幕经过导入器转换为 TextAsset。</summary>
        public string FindAssetType => "TextAsset";

        /// <summary>判断文件是否为外置字幕。</summary>
        /// <param name="data">资源采集上下文。</param>
        /// <returns>扩展名为 .srt 时返回 true。</returns>
        public bool IsCollectAsset(FilterRuleData data)
        {
            return string.Equals(Path.GetExtension(data.AssetPath), ".srt", StringComparison.OrdinalIgnoreCase);
        }
    }
}
