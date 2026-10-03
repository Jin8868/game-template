using AlloyFramework.UI;

namespace Game.UI
{
    public sealed partial class LoadingUIView : UIView
    {
        // <alloy-generated-bindings>
        // 此区块由 AlloyFramework UI 生成器维护，请勿手动修改。
        [UnityEngine.SerializeField, AlloyFramework.UI.UIBindingReference]
        private UnityEngine.UI.Image m_imgBg;

        public UnityEngine.UI.Image ImgBg => m_imgBg;

        [UnityEngine.SerializeField, AlloyFramework.UI.UIBindingReference]
        private UnityEngine.UI.Slider m_sliderProgress;

        public UnityEngine.UI.Slider SliderProgress => m_sliderProgress;

        [UnityEngine.SerializeField, AlloyFramework.UI.UIBindingReference]
        private UnityEngine.UI.Text m_txtLoding;

        public UnityEngine.UI.Text TxtLoding => m_txtLoding;

        [UnityEngine.SerializeField, AlloyFramework.UI.UIBindingReference]
        private UnityEngine.UI.Text m_txtPercent;

        public UnityEngine.UI.Text TxtPercent => m_txtPercent;
        // </alloy-generated-bindings>
    }
}
