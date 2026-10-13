using System.IO;
using Game.Video;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    [InitializeOnLoad]
    internal static class PulseFirstFrameSceneTools
    {
        private const string TESTSCENEPATH = "Assets/Scenes/PulsePlaybackTest.unity"; // 首帧测试场景资产路径。
        private const string SKIPSTARTUPKEY = "AlloyFramework.SkipAutomaticStartup"; // 编辑器独立测试启动开关。

        static PulseFirstFrameSceneTools()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // 进入 Play 前确定实际启动场景；重新编译后通过 SessionState 保持本次选择。
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                SceneAsset startScene = EditorSceneManager.playModeStartScene;
                string startPath = startScene != null
                    ? AssetDatabase.GetAssetPath(startScene)
                    : UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
                SessionState.SetBool(SKIPSTARTUPKEY, startPath == TESTSCENEPATH);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(SKIPSTARTUPKEY, false);
            }
        }

        private static void SetTestStartScene()
        {
            // 覆盖框架 F5 留下的 Boot 启动场景；随后按 Unity 的 Play 按钮运行测试。
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TESTSCENEPATH);
        }

        [MenuItem("★AlloyFramework★/视频/创建或打开首帧测试场景")]
        private static void CreateOrOpenScene()
        {
            // 菜单由用户主动执行，先提供当前场景保存机会再切换到独立测试环境。
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(TESTSCENEPATH))
            {
                EditorSceneManager.OpenScene(TESTSCENEPATH);
                SetTestStartScene();
                return;
            }
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraRoot = new GameObject("Pulse Test Camera", typeof(Camera));
            var camera = cameraRoot.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            cameraRoot.tag = "MainCamera";

            var canvasRoot = new GameObject("Pulse First Frame Test",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var imageRoot = new GameObject("Video Surface", typeof(RectTransform),
                typeof(RawImage), typeof(AspectRatioFitter));
            imageRoot.transform.SetParent(canvasRoot.transform, false);
            var imageRect = imageRoot.GetComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0.05f, 0.1f);
            imageRect.anchorMax = new Vector2(0.95f, 0.85f);
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            var image = imageRoot.GetComponent<RawImage>();
            image.raycastTarget = false;
            var fitter = imageRoot.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;
            var view = canvasRoot.AddComponent<PulseFirstFrameView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("m_targetImage").objectReferenceValue = image;
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, TESTSCENEPATH);
            SetTestStartScene();
            Selection.activeGameObject = canvasRoot;
            Debug.Log("[Pulse] 首帧测试场景已创建。先构建 Windows 原生 DLL，再进入 Play 模式。");
        }

        [MenuItem("★AlloyFramework★/视频/创建或打开首帧测试场景", true)]
        private static bool CanCreateScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
