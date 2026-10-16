using System;
using System.Diagnostics;
using System.Threading;

namespace Game.Video
{
    /// <summary>仅记录播放生命周期结果，不记录逐步骤测试数据或耗时。</summary>
    internal sealed class VideoPlaybackTrace
    {
        private static int m_nextID; // 区分多个播放器并发执行的操作。
        private readonly int m_id; // 本次操作编号。
        private readonly string m_operation; // 播放、定位、暂停、恢复或关闭。
        private bool m_finished; // 每次操作仅输出一次最终结果。

        internal VideoPlaybackTrace(string operation)
        {
            m_id = Interlocked.Increment(ref m_nextID);
            m_operation = operation;
            LogStarted();
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void LogStarted()
        {
            // 高频控制只输出最终结果，资源播放及关闭保留开始通知。
            if (m_operation == "播放准备" || m_operation == "关闭")
                UnityEngine.Debug.Log($"[Pulse][{m_id}] {m_operation}开始。");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        internal void Complete()
        {
            if (m_finished) { return; }
            m_finished = true;
            UnityEngine.Debug.Log($"[Pulse][{m_id}] {m_operation}完成。");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        internal void Fail(Exception exception)
        {
            if (m_finished) { return; }
            m_finished = true;
            if (exception is OperationCanceledException)
                UnityEngine.Debug.Log($"[Pulse][{m_id}] {m_operation}已取消。");
            else
                UnityEngine.Debug.LogError($"[Pulse][{m_id}] {m_operation}失败：{exception.Message}");
        }
    }
}
