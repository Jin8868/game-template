using UnityEditor;

namespace Game.Editor
{
    /// <summary>在 Boot 对象上直接编辑启动层和构建工具共用的配置。</summary>
    [CustomEditor(typeof(GameBootConfiguration))]
    public sealed class GameBootConfigurationEditor : UnityEditor.Editor
    {
        private UnityEditor.Editor m_settingsEditor; // 共用配置资产的缓存编辑器。

        /// <summary>绘制配置引用与模式、服务器和入口字段。</summary>
        public override void OnInspectorGUI()
        {
            // 引用固定到主包共用资产，避免场景配置与场景加载前的启动配置分叉。
            EditorGUI.BeginDisabledGroup(true);
            DrawDefaultInspector();
            EditorGUI.EndDisabledGroup();
            var boot = (GameBootConfiguration)target;
            if (boot.Settings == null)
            {
                EditorGUILayout.HelpBox("请选择 Resources/GameHotUpdateSettings 配置资产。", MessageType.Error);
                return;
            }
            EditorGUILayout.HelpBox("修改共用 Boot 配置；构建主包后，运行模式和服务器地址随主包固定。",
                MessageType.Info);
            CreateCachedEditor(boot.Settings, null, ref m_settingsEditor);
            m_settingsEditor.OnInspectorGUI();
        }

        private void OnDisable()
        {
            if (m_settingsEditor != null)
            {
                DestroyImmediate(m_settingsEditor);
            }
        }
    }
}
