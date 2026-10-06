using AlloyFramework.UI;

namespace Game.UI
{
    public sealed partial class NavigationDetailUIView : UIView
    {
        // <alloy-generated-bindings>
        // 此区块由 AlloyFramework UI 生成器维护，请勿手动修改。
        [UnityEngine.SerializeField, AlloyFramework.UI.UIBindingReference]
        private UnityEngine.UI.Button m_btnBack;

        public UnityEngine.UI.Button BtnBack => m_btnBack;

        [UnityEngine.SerializeField, AlloyFramework.UI.UIBindingReference]
        private UnityEngine.UI.Button m_btnOpenDetail;

        public UnityEngine.UI.Button BtnOpenDetail => m_btnOpenDetail;

        [UnityEngine.SerializeField, AlloyFramework.UI.UIBindingReference]
        private UnityEngine.UI.Button m_btnSkipBack;

        public UnityEngine.UI.Button BtnSkipBack => m_btnSkipBack;

        [UnityEngine.SerializeField, AlloyFramework.UI.UIBindingReference]
        private UnityEngine.UI.Text m_txtStatus;

        public UnityEngine.UI.Text TxtStatus => m_txtStatus;
        // </alloy-generated-bindings>

        // <alloy-generated-animation-keys>
        // 此区块由 AlloyFramework UI 生成器维护，请勿手动修改。
        public static class AnimationKeys
        {
        }
        // </alloy-generated-animation-keys>
    }
}
