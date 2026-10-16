using System;
using System.IO;
using AlloyFramework;

namespace Game.Video
{
    /// <summary>视频文件的宿主租约，必须在原生播放器完全关闭后释放。</summary>
    internal sealed class VideoResourceLease : IDisposable
    {
        private readonly RawFileLease m_rawFile; // 播放期间持有的原始文件读取租约。
        private readonly string m_ownedDirectory; // CDN 下载拥有的临时目录，本地文件为空。
        private bool m_isDisposed; // 避免重复删除目录。

        internal string FilePath { get; }

        internal VideoResourceLease(string filePath, RawFileLease rawFile = null, string ownedDirectory = null)
        {
            FilePath = filePath;
            m_rawFile = rawFile;
            m_ownedDirectory = ownedDirectory;
        }

        /// <summary>归还原始文件租约或释放下载文件，不删除借用的源文件。</summary>
        public void Dispose()
        {
            if (m_isDisposed) return;
            m_isDisposed = true;
            m_rawFile?.Dispose();
            if (!string.IsNullOrEmpty(m_ownedDirectory) && Directory.Exists(m_ownedDirectory))
                Directory.Delete(m_ownedDirectory, true);
        }
    }
}
