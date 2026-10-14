using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using PulseVideoPlayer;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.Profiling;
#endif
using UnityEngine.UI;

namespace Game.Video
{
    /// <summary>用于 Windows 连续播放、暂停与原生资源验证的业务展示组件。</summary>
    public sealed class PulseFirstFrameView : MonoBehaviour
    {
        private static readonly int m_rgbRectID = Shader.PropertyToID("_RGBRect"); // RGB 区域参数。
        private static readonly int m_alphaRectID = Shader.PropertyToID("_AlphaRect"); // Alpha 区域参数。
        private static readonly int m_hasAlphaID = Shader.PropertyToID("_HasAlpha"); // 透明模式参数。
        private static readonly int m_unpremultiplyID = Shader.PropertyToID("_Unpremultiply"); // 去预乘参数。
        [SerializeField] private RawImage m_targetImage; // 视频纹理显示对象。
        [SerializeField] private string m_relativePath = "PulseTests/Synthetic/all_in_one.pulse"; // 默认帧号测试源。
        [SerializeField] private bool m_openAutomatically = true; // 进入运行模式时自动打开。
        [SerializeField] private bool m_playAutomatically = true; // 准备完成后自动开始连续播放。
        private PulsePlayer m_player; // 当前播放器实例。
        private Material m_material; // 当前视频的区域采样材质。
        private CancellationTokenSource m_openCancellation; // 展示组件打开操作的取消源。
        private int m_generation; // 防止旧的异步操作重新绑定界面。
        private string m_status = "等待打开首帧。"; // 测试面板状态文本。
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string m_resourceStatus = "等待资源采样。"; // 缓存的界面资源统计。
        private double m_nextResourceSample; // 下一次低频资源采样时间。
        private bool m_nativeDiagnosticsUnavailable; // 缺少新版 DLL 时避免反复触发入口查找异常。
        private GUIStyle m_buttonStyle; // 重用的加大按钮样式。
        private GUIStyle m_labelStyle; // 重用的统计文本样式。
        private string m_playbackStatus = "尚未打开视频。"; // 低频更新的时间与帧序号信息。
        private double m_nextPlaybackSample; // 下一次播放状态文本采样时间。
#endif

        private void OnEnable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Application.isPlaying) LogResources("测试开始");
#endif
            if (Application.isPlaying && m_openAutomatically) Open();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (now >= m_nextPlaybackSample)
            {
                m_nextPlaybackSample = now + 0.1;
                m_playbackStatus = m_player == null ? "尚未打开视频。" :
                    $"状态 {m_player.State}，时间 {m_player.GetPlayTimeMs() / 1000:F2}" +
                    $" / {m_player.DurationSeconds:F2} 秒\n已提交帧 {m_player.PresentedFrame}，" +
                    $"纹理版本 {m_player.TextureVersion}，追赶丢帧 {m_player.DroppedFrames}";
            }
            // 低频采样并缓存显示文本，不逐帧打印，避免诊断本身制造大量日志分配。
            if (Time.realtimeSinceStartupAsDouble < m_nextResourceSample) return;
            m_nextResourceSample = Time.realtimeSinceStartupAsDouble + 1;
            m_resourceStatus = ReadResourceStatus();
        }
#endif

        /// <summary>取消旧展示并打开视频，按配置决定准备完成后是否自动播放。</summary>
        public void Open()
        {
            OpenCore(m_playAutomatically);
        }

        private void OpenCore(bool playAfterReady)
        {
            if (!Application.isPlaying) return;
            Close();
            if (m_targetImage == null)
            {
                m_status = "请先绑定 RawImage。";
                Debug.LogError("[Pulse] 首帧测试缺少 RawImage。", this);
                return;
            }
            var player = new PulsePlayer();
            var cancellation = new CancellationTokenSource();
            int generation = ++m_generation;
            m_player = player;
            player.Started += OnStarted;
            player.Paused += OnPaused;
            player.Resumed += OnResumed;
            player.Completed += OnCompleted;
            player.Failed += OnPlaybackFailed;
            m_openCancellation = cancellation;
            m_status = "正在读取、解码并提交首帧……";
            OpenCoreAsync(player, generation, cancellation, playAfterReady).Forget(LogUnhandled);
        }

        /// <summary>开始播放、从暂停恢复，或在结束后重新打开文件重播。</summary>
        public void PlayOrResume()
        {
            if (m_player == null) return;
            try
            {
                switch (m_player.State)
                {
                    case EPulsePlayerState.Ready:
                        m_player.Play();
                        break;
                    case EPulsePlayerState.Paused:
                        m_player.Resume();
                        break;
                    case EPulsePlayerState.Ended:
                        OpenCore(true);
                        break;
                }
            }
            catch (Exception exception)
            {
                OnPlaybackFailed(exception);
            }
        }

        /// <summary>冻结当前播放时间并保留画面。</summary>
        public void Pause()
        {
            if (m_player == null || m_player.State != EPulsePlayerState.Playing) return;
            try
            {
                m_player.Pause();
            }
            catch (Exception exception)
            {
                OnPlaybackFailed(exception);
            }
        }

        /// <summary>解除画面绑定并请求异步回收当前会话。</summary>
        public void Close()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool hadPlayer = m_player != null;
#endif
            m_generation++;
            m_openCancellation?.Cancel();
            m_openCancellation = null;
            // 先解除 UI 使用关系，再释放播放器与材质。
            if (m_targetImage != null)
            {
                m_targetImage.texture = null;
                m_targetImage.material = null;
            }
            if (m_player != null)
            {
                // 旧会话不能继续向展示组件发送事件。
                m_player.Started -= OnStarted;
                m_player.Paused -= OnPaused;
                m_player.Resumed -= OnResumed;
                m_player.Completed -= OnCompleted;
                m_player.Failed -= OnPlaybackFailed;
                m_player.Dispose();
            }
            m_player = null;
            if (m_material != null) Destroy(m_material);
            m_material = null;
            m_status = "已关闭，原生资源在后台退役。";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (hadPlayer && Application.isPlaying)
            {
                LogResources("关闭已请求");
                LogClosedResourcesAsync(m_generation).Forget(LogUnhandled);
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private async UniTask LogClosedResourcesAsync(int generation)
        {
            // 关闭需要帧末 GPU 围栏，延迟采样；已重新打开时不把新会话误标为关闭结果。
            await UniTask.Delay(TimeSpan.FromSeconds(1), ignoreTimeScale: true);
            if (this == null || generation != m_generation) return;
            LogResources("关闭后 1 秒（采样，不保证退役已完成）");
        }

        private string ReadResourceStatus()
        {
            const double BYTESPERMIB = 1024d * 1024d;
            double allocated = Profiler.GetTotalAllocatedMemoryLong() / BYTESPERMIB;
            double reserved = Profiler.GetTotalReservedMemoryLong() / BYTESPERMIB;
            double managed = Profiler.GetMonoUsedSizeLong() / BYTESPERMIB;
            string memory = $"Unity 已分配 {allocated:F1} MiB，保留 {reserved:F1} MiB，托管已用 {managed:F1} MiB";
            if (m_nativeDiagnosticsUnavailable) return memory + "\n原生统计不可用，请重新生成 DLL。";
            try
            {
                PulseResourceSnapshot resources = PulseDiagnostics.ReadResources();
                return memory +
                    $"\n原生会话 {resources.Sessions}，待关闭 {resources.ClosingSessions}，" +
                    $"线程句柄 {resources.WorkerHandles}，输出纹理 {resources.OutputTextures}，" +
                    $"持有解码表面 {resources.DecoderSurfaces}" +
                    $"\n输出纹理估算 {resources.EstimatedOutputBytes / BYTESPERMIB:F2} MiB（非完整显存统计）";
            }
            catch (DllNotFoundException)
            {
                m_nativeDiagnosticsUnavailable = true;
            }
            catch (EntryPointNotFoundException)
            {
                m_nativeDiagnosticsUnavailable = true;
            }
            catch (PlatformNotSupportedException)
            {
                m_nativeDiagnosticsUnavailable = true;
            }
            return memory + "\n原生统计不可用，请重新生成 Windows DLL。";
        }

        private void LogResources(string phase)
        {
            m_resourceStatus = ReadResourceStatus();
            Debug.Log($"[Pulse][资源] {phase}\n{m_resourceStatus}", this);
        }
#endif

        private async UniTask OpenCoreAsync(
            PulsePlayer player, int generation, CancellationTokenSource cancellation, bool playAfterReady)
        {
            try
            {
                string filePath = Path.Combine(Application.streamingAssetsPath, m_relativePath);
                await player.OpenAsync(filePath, cancellation.Token);
                if (this == null || !isActiveAndEnabled || generation != m_generation) return;
                Shader shader = Resources.Load<Shader>("PulseVideoSurface");
                if (shader == null) throw new InvalidOperationException("未找到 PulseVideoSurface Shader。");
                PulseMetadata metadata = player.Metadata;
                m_material = new Material(shader);
                m_material.name = "Pulse First Frame Material";
                m_material.SetVector(m_rgbRectID, GetUVRegion(metadata.RGBRegion, metadata));
                m_material.SetFloat(m_hasAlphaID, metadata.AlphaRegion.HasValue ? 1 : 0);
                m_material.SetFloat(m_unpremultiplyID,
                    metadata.AlphaMode == EPulseAlphaMode.Premultiplied ? 1 : 0);
                if (metadata.AlphaRegion.HasValue)
                {
                    m_material.SetVector(m_alphaRectID, GetUVRegion(metadata.AlphaRegion.Value, metadata));
                }
                m_targetImage.material = m_material;
                m_targetImage.texture = player.Texture;
                var fitter = m_targetImage.GetComponent<AspectRatioFitter>();
                if (fitter != null) fitter.aspectRatio = (float)metadata.DisplayWidth / metadata.DisplayHeight;
                m_status = $"首帧就绪：{metadata.DisplayWidth}×{metadata.DisplayHeight}，Alpha {metadata.AlphaMode}。";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log(
                    $"[Pulse] {m_status} 原始首帧 PTS {player.FirstFrameTimestamp}（100ns）。", this);
                LogResources("首帧就绪");
#endif
                if (playAfterReady) player.Play();
            }
            catch (OperationCanceledException)
            {
                if (this != null && generation == m_generation) m_status = "打开已取消。";
            }
            catch (Exception exception)
            {
                if (this != null && generation == m_generation)
                {
                    Close();
                    m_status = exception.Message;
                    Debug.LogException(exception, this);
                }
            }
            finally
            {
                if (ReferenceEquals(m_openCancellation, cancellation)) m_openCancellation = null;
                cancellation.Dispose();
            }
        }

        private void OnStarted()
        {
            m_status = "正在播放。";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Pulse] 开始连续播放，时钟从零开始。", this);
#endif
        }

        private void OnPaused()
        {
            m_status = "已暂停，播放时钟保持不变。";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Pulse] 暂停于 {m_player.GetPlayTimeMs() / 1000:F3} 秒。", this);
#endif
        }

        private void OnResumed()
        {
            m_status = "已恢复播放。";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Pulse] 从 {m_player.GetPlayTimeMs() / 1000:F3} 秒继续播放。", this);
#endif
        }

        private void OnCompleted()
        {
            m_status = "播放结束，保留末帧。可以重新播放或关闭。";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Pulse] 播放结束，末帧 {m_player.PresentedFrame}。", this);
            LogResources("播放结束（仍保留输出纹理，关闭后才释放）");
#endif
        }

        private void OnPlaybackFailed(Exception exception)
        {
            Close();
            m_status = exception.Message;
            Debug.LogException(exception, this);
        }

        private static Vector4 GetUVRegion(PulseRegion region, PulseMetadata metadata)
        {
            return new Vector4(
                (float)region.X / metadata.EncodedWidth, (float)region.Y / metadata.EncodedHeight,
                (float)region.Width / metadata.EncodedWidth, (float)region.Height / metadata.EncodedHeight);
        }

        private static void LogUnhandled(Exception exception)
        {
            Debug.LogException(exception);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (m_buttonStyle == null)
            {
                m_buttonStyle = new GUIStyle(GUI.skin.button);
                m_buttonStyle.fontSize = 24;
                m_labelStyle = new GUIStyle(GUI.skin.label);
                m_labelStyle.fontSize = 18;
                m_labelStyle.wordWrap = true;
            }
            // 仅供手动验证打开、关闭与重开，不作为正式业务 UI。
            float panelWidth = Mathf.Min(480, Screen.width * 0.4f);
            float buttonWidth = Mathf.Max(60, (panelWidth - 36) * 0.5f);
            m_buttonStyle.fontSize = Mathf.Clamp((int)(buttonWidth / 220 * 24), 14, 24);
            GUI.Box(new Rect(4, 4, panelWidth, 460), string.Empty);
            if (GUI.Button(new Rect(12, 12, buttonWidth, 60), "打开视频", m_buttonStyle)) Open();
            if (GUI.Button(new Rect(buttonWidth + 24, 12, buttonWidth, 60),
                "关闭播放器", m_buttonStyle)) Close();
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && m_player != null &&
                (m_player.State == EPulsePlayerState.Ready || m_player.State == EPulsePlayerState.Paused ||
                m_player.State == EPulsePlayerState.Ended);
            string playLabel = m_player?.State == EPulsePlayerState.Ended ? "重新播放" : "播放 / 恢复";
            if (GUI.Button(new Rect(12, 84, buttonWidth, 60), playLabel, m_buttonStyle)) PlayOrResume();
            GUI.enabled = wasEnabled && m_player?.State == EPulsePlayerState.Playing;
            if (GUI.Button(new Rect(buttonWidth + 24, 84, buttonWidth, 60), "暂停", m_buttonStyle)) Pause();
            GUI.enabled = wasEnabled;
            GUI.Label(new Rect(12, 152, panelWidth - 24, 60), m_status, m_labelStyle);
            GUI.Label(new Rect(12, 216, panelWidth - 24, 80), m_playbackStatus, m_labelStyle);
            // 仅显示进度，不使用可拖动控件，避免让界面承诺尚未实现的 Seek。
            float progress = m_player == null || m_player.DurationSeconds <= 0 ? 0 :
                (float)(m_player.GetPlayTimeMs() / (m_player.DurationSeconds * 1000));
            GUI.Box(new Rect(12, 300, panelWidth - 24, 16), string.Empty);
            if (progress > 0)
            {
                GUI.Box(new Rect(12, 300, (panelWidth - 24) * Mathf.Clamp01(progress), 16), string.Empty);
            }
            GUI.Label(new Rect(12, 326, panelWidth - 24, 130), m_resourceStatus, m_labelStyle);
        }
#endif

        private void OnDisable()
        {
            Close();
        }
    }
}
