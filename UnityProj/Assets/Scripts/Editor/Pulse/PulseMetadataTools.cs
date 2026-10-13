using System;
using System.IO;
using PulseVideoPlayer;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    internal static class PulseMetadataTools
    {
        private static readonly string[] m_samplePaths =
        {
            "Synthetic/all_in_one.pulse",
            "Alpha/alpha_test.pulse",
            "Sintel/Sintel.2010.1080p.pulse"
        }; // 工程内三组本地测试文件的相对路径。

        [MenuItem("★AlloyFramework★/视频/读取Pulse测试元数据")]
        private static void ReadTestMetadata()
        {
            // 在编辑器中独立检查容器读取，无需先启动游戏业务流程。
            string sampleRoot = Path.Combine(Application.streamingAssetsPath, "PulseTests");
            int succeeded = 0;
            foreach (string relativePath in m_samplePaths)
            {
                string filePath = Path.Combine(sampleRoot, relativePath);
                try
                {
                    PulseMetadata metadata = PulseMetadataReader.Read(filePath);
                    Debug.Log(
                        $"[Pulse] {relativePath}：" +
                        $"编码 {metadata.EncodedWidth}×{metadata.EncodedHeight}，" +
                        $"显示 {metadata.DisplayWidth}×{metadata.DisplayHeight}，" +
                        $"帧数 {metadata.FrameCount}，帧率 {metadata.FrameRate.Value:F3}，" +
                        $"时长 {metadata.DurationSeconds:F3}s，Alpha {metadata.AlphaMode}。");
                    succeeded++;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Pulse] 读取失败：{relativePath}\n{exception}");
                }
            }
            Debug.Log($"[Pulse] 元数据读取检查完成：{succeeded}/{m_samplePaths.Length} 成功。");
        }
    }
}
