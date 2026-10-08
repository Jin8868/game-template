using AlloyFramework.UI;

namespace Game.UI
{
    public sealed partial class NavigationHomeUIView : UIView
    {
        // <alloy-generated-bindings>
        // 此区块由 AlloyFramework UI 生成器维护，请勿手动修改。
        [UnityEngine.SerializeField, AlloyFramework.UI.UIBindingReference]
        private UnityEngine.UI.Button m_btnOpenDetail;

        public UnityEngine.UI.Button BtnOpenDetail => m_btnOpenDetail;

        [UnityEngine.SerializeField, AlloyFramework.UI.UIBindingReference]
        private UnityEngine.UI.Button m_btnOpenPopup;

        public UnityEngine.UI.Button BtnOpenPopup => m_btnOpenPopup;

        [UnityEngine.SerializeField, AlloyFramework.UI.UIBindingReference]
        private UnityEngine.UI.Text m_txtStatus;

        public UnityEngine.UI.Text TxtStatus => m_txtStatus;
        // </alloy-generated-bindings>

        public float Volume;
    }
}
