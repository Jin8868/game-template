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
    /// <summary>用于 Windows 原生首帧链路验证的业务展示组件。</summary>
    public sealed class PulseFirstFrameView : MonoBehaviour
    {
        private static readonly int m_rgbRectID = Shader.PropertyToID("_RGBRect"); // RGB 区域参数。
        private static readonly int m_alphaRectID = Shader.PropertyToID("_AlphaRect"); // Alpha 区域参数。
        private static readonly int m_hasAlphaID = Shader.PropertyToID("_HasAlpha"); // 透明模式参数。
        private static readonly int m_unpremultiplyID = Shader.PropertyToID("_Unpremultiply"); // 去预乘参数。
        [SerializeField] private RawImage m_targetImage; // 首帧纹理显示对象。
        [SerializeField] private string m_relativePath = "PulseTests/Synthetic/all_in_one.pulse"; // 默认帧号测试源。
        [SerializeField] private bool m_openAutomatically = true; // 进入运行模式时自动打开。
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
            // 低频采样并缓存显示文本，不逐帧打印，避免诊断本身制造大量日志分配。
            if (Time.realtimeSinceStartupAsDouble < m_nextResourceSample) return;
            m_nextResourceSample = Time.realtimeSinceStartupAsDouble + 1;
            m_resourceStatus = ReadResourceStatus();
        }
#endif

        /// <summary>取消旧展示并打开指定视频的首帧。</summary>
        public void Open()
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
            m_openCancellation = cancellation;
            m_status = "正在读取、解码并提交首帧……";
            OpenCoreAsync(player, generation, cancellation).Forget(LogUnhandled);
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
            m_player?.Dispose();
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
            PulsePlayer player, int generation, CancellationTokenSource cancellation)
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
                    $"[Pulse] {m_status} 当前仅显示首帧，原始 PTS {player.FirstFrameTimestamp}（100ns）。", this);
                LogResources("首帧就绪");
#endif
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
            float buttonWidth = Mathf.Min(220, (Screen.width - 36) * 0.5f);
            if (GUI.Button(new Rect(12, 12, buttonWidth, 60), "打开首帧", m_buttonStyle)) Open();
            if (GUI.Button(new Rect(buttonWidth + 24, 12, buttonWidth, 60),
                "关闭播放器", m_buttonStyle)) Close();
            GUI.Label(new Rect(12, 80, Screen.width - 24, 52), m_status, m_labelStyle);
            GUI.Label(new Rect(12, 136, Screen.width - 24, 130), m_resourceStatus, m_labelStyle);
        }
#endif

        private void OnDisable()
        {
            Close();
        }
    }
}
