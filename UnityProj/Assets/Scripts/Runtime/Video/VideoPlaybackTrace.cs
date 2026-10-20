using System;
using System.Diagnostics;
using System.Threading;
using PulseVideoPlayer;

namespace Game.Video
{
    /// <summary>将播放准备的阶段耗时汇总到单条日志，其他操作保留开发日志。</summary>
    internal sealed class VideoPlaybackTrace
    {
        private static int m_nextID; // 区分多个播放器并发执行的操作。
        private readonly int m_id; // 本次操作编号。
        private readonly string m_operation; // 播放、定位、暂停、恢复或关闭。
        private readonly PulsePreparationTimings m_timings; // 仅播放准备记录完整阶段耗时。
        private bool m_finished; // 每次操作仅输出一次最终结果。

        internal PulsePreparationTimings Timings => m_timings;

        internal VideoPlaybackTrace(string operation)
        {
            m_id = Interlocked.Increment(ref m_nextID);
            m_operation = operation;
            if (operation == "播放准备") m_timings = new PulsePreparationTimings();
            LogStarted();
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void LogStarted()
        {
            // 播放准备只输出一条汇总；关闭仍保留开发环境的开始通知。
            if (m_operation == "关闭")
                UnityEngine.Debug.Log($"[Pulse][{m_id}] {m_operation}开始。");
        }

        internal void Complete()
        {
            if (m_finished) { return; }
            m_finished = true;
            if (m_timings != null)
                UnityEngine.Debug.Log($"[Pulse耗时][{m_id}] 播放准备完成 | {m_timings.Finish()}");
            else LogResult("完成");
        }

        internal void Fail(Exception exception)
        {
            if (m_finished) { return; }
            m_finished = true;
            if (m_timings != null)
            {
                // 普通包也输出失败前的阶段耗时，保持一次播放只有一条耗时汇总。
                string result = exception is OperationCanceledException ? "取消" : "失败";
                string error = exception.Message.Replace('\r', ' ').Replace('\n', ' ');
                UnityEngine.Debug.LogWarning(
                    $"[Pulse耗时][{m_id}] 播放准备{result} | {m_timings.Finish()} | 原因={error}");
                return;
            }
            LogFailure(exception);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void LogResult(string result)
        {
            UnityEngine.Debug.Log($"[Pulse][{m_id}] {m_operation}{result}。");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void LogFailure(Exception exception)
        {
            if (exception is OperationCanceledException)
                UnityEngine.Debug.Log($"[Pulse][{m_id}] {m_operation}已取消。");
            else
                UnityEngine.Debug.LogError($"[Pulse][{m_id}] {m_operation}失败：{exception.Message}");
        }
    }
}
