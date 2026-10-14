using AlloyFramework.UI;

namespace Game.UI
{
    public sealed partial class VideoPreviewUIView : UIView
    {
        // <alloy-generated-bindings>
        // 此区块由 AlloyFramework UI 生成器维护，请勿手动修改。
        [UnityEngine.SerializeField, AlloyFramework.UI.UIBindingReference]
        private UnityEngine.UI.Image m_imgLoadPercent;

        public UnityEngine.UI.Image ImgLoadPercent => m_imgLoadPercent;

        [UnityEngine.SerializeField, AlloyFramework.UI.UIBindingReference]
        private UnityEngine.UI.Text m_txtPercent;

        public UnityEngine.UI.Text TxtPercent => m_txtPercent;

        [UnityEngine.SerializeField, AlloyFramework.UI.UIBindingReference]
        private UnityEngine.UI.Slider m_sliderTime;

        public UnityEngine.UI.Slider SliderTime => m_sliderTime;

        [UnityEngine.SerializeField, AlloyFramework.UI.UIBindingReference]
        private UnityEngine.UI.RawImage m_rawVideo;

        public UnityEngine.UI.RawImage RawVideo => m_rawVideo;

        [UnityEngine.SerializeField, AlloyFramework.UI.UIBindingReference]
        private UnityEngine.UI.Text m_txtTime;

        public UnityEngine.UI.Text TxtTime => m_txtTime;
        // </alloy-generated-bindings>

        // <alloy-generated-animation-keys>
        // 此区块由 AlloyFramework UI 生成器维护，请勿手动修改。
        public static class AnimationKeys
        {
        }
        // </alloy-generated-animation-keys>
    }
}
