using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using AlloyFramework.Audio;
using Cysharp.Threading.Tasks;
using PulseVideoPlayer;

namespace Game.Video
{
    /// <summary>在业务热更层旁路记录音频资源准备，保留原交付器的加载、校验与租约行为。</summary>
    internal sealed class VideoAudioPreparationTrace : IAudioContentProvider, IDisposable
    {
        private readonly Stopwatch m_clock = Stopwatch.StartNew(); // 覆盖探针安装和预加载等待的总时间。
        private readonly PulsePreparationTimings m_timings; // 将诊断附加到本次播放的唯一汇总日志。
        private readonly HashSet<string> m_groupKeys = new HashSet<string>(); // 只统计目标事件及其共享依赖。
        private readonly StringBuilder m_groups = new StringBuilder(); // 各资源组的耗时与制作清单信息。
        private readonly FieldInfo m_providerField; // 旧安装包没有诊断接口，仅读写已保留的交付器字段。
        private readonly IAudioContentProvider m_source; // 原交付器，全部加载行为仍由其执行。
        private double m_resourceMilliseconds; // 本次观测到的资源准备累计时间，不含清单描述开销。
        private int m_observedGroups; // 实际经过交付器的目标组数量。
        private bool m_installed; // 仅成功安装时才尝试恢复。
        private bool m_disposed; // 已结束的探针只负责透明转发，不继续修改汇总。
        private string m_unavailableReason; // 诊断不可用不影响正常播放。

        public AudioContentManifest Manifest => m_source.Manifest;

        internal VideoAudioPreparationTrace(string audioEventKey, PulsePreparationTimings timings)
        {
            m_timings = timings;
            try
            {
                // 只替换交付入口，不重装音频引擎，也不更换原生后端。
                m_providerField = typeof(AudioManager).GetField(
                    "m_provider", BindingFlags.Instance | BindingFlags.NonPublic);
                if (m_providerField == null)
                {
                    m_unavailableReason = "安装包缺少交付器字段元数据";
                    return;
                }
                m_source = m_providerField.GetValue(AudioManager.Instance) as IAudioContentProvider;
                if (m_source == null)
                {
                    m_unavailableReason = "音频交付器尚未安装";
                    return;
                }
                CollectGroups(audioEventKey);
                if (m_groupKeys.Count == 0)
                {
                    m_unavailableReason = "清单中未找到目标事件依赖";
                    return;
                }
                m_providerField.SetValue(AudioManager.Instance, this);
                m_installed = true;
            }
            catch (Exception exception)
            {
                m_unavailableReason = "探针安装失败：" + exception.GetType().Name;
            }
        }

        /// <summary>透明转发资源组准备，仅为目标事件的依赖记录耗时。</summary>
        /// <param name="groupKey">原始资源组名称。</param>
        /// <param name="settings">当前音频设置，不修改校验或缓存策略。</param>
        /// <param name="cancellationToken">原始加载令牌。</param>
        /// <returns>原交付器创建的同一份资源租约。</returns>
        public async UniTask<AudioContentLease> PrepareGroupAsync(
            string groupKey, AudioSettings settings, CancellationToken cancellationToken)
        {
            if (m_disposed || !m_groupKeys.Contains(groupKey))
                return await m_source.PrepareGroupAsync(groupKey, settings, cancellationToken);

            var clock = Stopwatch.StartNew();
            AudioContentLease content = null;
            try
            {
                content = await m_source.PrepareGroupAsync(groupKey, settings, cancellationToken);
                return content;
            }
            finally
            {
                clock.Stop();
                if (!m_disposed)
                {
                    // 记录失败或取消前已经消耗的时间，不捕获或替换原始加载异常。
                    m_resourceMilliseconds += clock.Elapsed.TotalMilliseconds;
                    m_observedGroups++;
                    AppendGroup(groupKey, content, settings, clock.Elapsed.TotalMilliseconds);
                }
            }
        }

        /// <summary>恢复原交付入口，并把已有观测合并到本次播放日志。</summary>
        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;
            m_clock.Stop();
            try
            {
                if (m_installed && ReferenceEquals(m_providerField.GetValue(AudioManager.Instance), this))
                {
                    // 并发播放可能形成短期包装链，跳过已经结束的探针，避免恢复失效入口。
                    IAudioContentProvider source = m_source;
                    while (source is VideoAudioPreparationTrace trace && trace.m_disposed)
                        source = trace.m_source;
                    m_providerField.SetValue(AudioManager.Instance, source);
                }
            }
            catch (Exception exception)
            {
                // 恢复失败时已结束的包装器仍透明转发，诊断不能打断音频清理。
                m_unavailableReason = "探针恢复失败：" + exception.GetType().Name;
            }

            if (!m_installed)
            {
                m_timings.AddDetail("音轨明细", "不可用：" + m_unavailableReason);
                return;
            }
            double remaining = Math.Max(0, m_clock.Elapsed.TotalMilliseconds - m_resourceMilliseconds);
            var summary = new StringBuilder();
            summary.Append("资源准备合计=").Append(FormatMilliseconds(m_resourceMilliseconds))
                .Append("; 其余等待(含Wwise及依赖)=").Append(FormatMilliseconds(remaining))
                .Append("; 观测资源组数=").Append(m_observedGroups);
            if (m_observedGroups == 0) summary.Append("; 未触发资源准备(可能已缓存或加载在途)");
            summary.Append(m_groups);
            if (m_unavailableReason != null) summary.Append("; ").Append(m_unavailableReason);
            m_timings.AddDetail("音轨明细", summary.ToString());
        }

        private void CollectGroups(string audioEventKey)
        {
            AudioContentManifest manifest = m_source.Manifest;
            if (manifest?.Events == null) return;
            var pending = new Queue<string>();
            foreach (AudioEventDependency dependency in manifest.Events)
            {
                if (dependency.Key != audioEventKey || dependency.Groups == null) continue;
                foreach (string groupKey in dependency.Groups) pending.Enqueue(groupKey);
            }

            // 迭代收集传递依赖，避免循环配置造成递归溢出。
            while (pending.Count > 0)
            {
                string groupKey = pending.Dequeue();
                if (!m_groupKeys.Add(groupKey) || manifest.Groups == null) continue;
                foreach (AudioContentGroup group in manifest.Groups)
                {
                    if (group.Key != groupKey || group.Dependencies == null) continue;
                    foreach (string dependencyKey in group.Dependencies) pending.Enqueue(dependencyKey);
                }
            }
        }

        private void AppendGroup(
            string groupKey, AudioContentLease content, AudioSettings settings, double milliseconds)
        {
            m_groups.Append("; 组=").Append(groupKey).Append("{资源准备=")
                .Append(FormatMilliseconds(milliseconds));
            if (content == null)
            {
                m_groups.Append(", 未完成}");
                return;
            }

            // 文件仅查询长度，不额外读取媒体内容，不人为触发一次磁盘预热。
            AudioContentGroup group = content.Group;
            long bytes = 0;
            int apkPackages = 0;
            int diskPackages = 0;
            bool sizeAvailable = true;
            try
            {
                if (group.Files != null)
                {
                    foreach (AudioContentFile file in group.Files)
                    {
                        if (content.IsFilePackage(file.Path))
                        {
                            bytes += file.FilePackageSize;
                            if (content.GetFilePath(file.Path).StartsWith("jar:", StringComparison.OrdinalIgnoreCase))
                                apkPackages++;
                            else diskPackages++;
                        }
                        else bytes += new FileInfo(content.GetFilePath(file.Path)).Length;
                    }
                }
            }
            catch (Exception)
            {
                sizeAvailable = false;
            }
            m_groups.Append(", 文件数=").Append(group.Files?.Length ?? 0)
                .Append(", 大小MiB=").Append(sizeAvailable
                    ? (bytes / 1048576d).ToString("F1", CultureInfo.InvariantCulture) : "未知")
                .Append(", 包文件额外SHA256=").Append(settings.VerifyContentHashes ? "开" : "关")
                .Append(", Bank数=").Append(group.Banks?.Length ?? 0)
                .Append(", APK直读包数=").Append(apkPackages)
                .Append(", 磁盘包数=").Append(diskPackages)
                .Append(", 预备媒体事件数=").Append(group.PreparedEvents?.Length ?? 0).Append('}');
        }

        private static string FormatMilliseconds(double milliseconds)
        {
            return milliseconds.ToString("F1", CultureInfo.InvariantCulture) + "ms";
        }
    }
}
