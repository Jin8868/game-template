using System.IO;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>将外置 UTF-8 SRT 导入为 TextAsset，供普通资源包加载。</summary>
    [ScriptedImporter(1, "srt")]
    public sealed class PulseSubtitleImporter : ScriptedImporter
    {
        /// <summary>读取字幕源文件并建立唯一主资源。</summary>
        /// <param name="context">Unity 的导入上下文。</param>
        public override void OnImportAsset(AssetImportContext context)
        {
            string source = File.ReadAllText(context.assetPath, new UTF8Encoding(false, true));
            var asset = new TextAsset(source) { name = Path.GetFileNameWithoutExtension(context.assetPath) };
            context.AddObjectToAsset("Subtitle", asset);
            context.SetMainObject(asset);
        }
    }
}
