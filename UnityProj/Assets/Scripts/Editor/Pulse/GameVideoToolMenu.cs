using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Game.Editor
{
    internal static class GameVideoToolMenu
    {
        [MenuItem("★AlloyFramework★/视频/打开转码工具")]
        private static void OpenVideoTool()
        {
            // 工具随组件分发，由宿主工程提供符合业务菜单规范的入口。
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                EditorUtility.DisplayDialog("视频转码工具", "当前转码工具仅支持 Windows 编辑器。", "确定");
                return;
            }

            try
            {
                PackageInfo package = PackageInfo.FindForAssetPath("Packages/com.pulse.videoplayer/package.json");
                if (package == null)
                    throw new InvalidOperationException("未找到 com.pulse.videoplayer 组件包。");

                string executablePath = Path.Combine(package.resolvedPath, "Tools~", "encodeTool", "VideoTool.exe");
                if (!File.Exists(executablePath))
                    throw new FileNotFoundException("组件包中缺少转码工具，请检查组件是否完整。", executablePath);

                // 使用工具自身目录启动，确保离线依赖和日志定位一致。
                Process.Start(new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = Path.GetDirectoryName(executablePath),
                    UseShellExecute = true
                });
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                EditorUtility.DisplayDialog("无法打开转码工具", exception.Message, "确定");
            }
        }
    }
}
