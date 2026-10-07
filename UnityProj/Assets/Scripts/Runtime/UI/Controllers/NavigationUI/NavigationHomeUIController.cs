using System;
using AlloyFramework;
using AlloyFramework.Audio;
using AlloyFramework.UI;

namespace Game.UI
{
    public sealed class NavigationHomeUIController : UIController<NavigationHomeUIView>
    {
        private AudioScope m_audioScope; // 本界面音频的生命周期。

        protected override void OnCreate()
        {
            m_audioScope = AudioManager.Instance.CreateScope("NavigationHomeUI");
            View.BtnOpenDetail.onClick.AddListener(OpenDetail);
            View.BtnOpenPopup.onClick.AddListener(OpenPopup);
            View.AnimationEventReceived += OnAnimationEventReceived;
        }

        protected override void OnInitData(UIEmptyData data)
        {
            View.TxtStatus.text = "首页已打开，可验证 Push 和 Overlay。";
        }

        protected override void OnRefresh(UIEmptyData data)
        {
            View.TxtStatus.text = "首页已刷新。";
        }

        protected override void OnPause()
        {
            View.TxtStatus.text = "首页已暂停。";
        }

        protected override void OnResume()
        {
            View.TxtStatus.text = "首页已恢复，返回流程成功。";
        }

        protected override void OnDispose()
        {
            m_audioScope?.Dispose();
            m_audioScope = null;
            View.BtnOpenDetail.onClick.RemoveListener(OpenDetail);
            View.BtnOpenPopup.onClick.RemoveListener(OpenPopup);
            View.AnimationEventReceived -= OnAnimationEventReceived;
        }

        private void OpenDetail()
        {
            PlayButtonClick();
            View.TxtStatus.text = "正在以 Push 模式打开详情页……";
            UIManager.Instance.Jump(
                NavigationJumpID.DETAIL,
                onCompleted: OnJumpCompleted);
        }

        private void OpenPopup()
        {
            PlayButtonClick();
            View.TxtStatus.text = "正在以 Overlay 模式打开弹窗……";
            UIManager.Instance.Jump(
                NavigationJumpID.POPUP,
                onCompleted: OnJumpCompleted);
        }

        private void OnJumpCompleted(UIHandle handle, Exception exception)
        {
            if (exception != null)
            {
                View.TxtStatus.text = $"跳转失败：{exception.Message}";
                AlloyDebug.Error(exception);
                return;
            }

            View.TxtStatus.text = $"已打开 {handle.UIName}。";
        }

        private void PlayButtonClick()
        {
            // 业务只提供事件名称和归属，不管理 Bank、媒体或监听器。
            AudioManager.Instance.PlayAudio("Play_ButtonClick", new AudioPlayOptions { Scope = m_audioScope });
        }

        private void OnAnimationEventReceived(UIAnimationEventContext context)
        {
            View.TxtStatus.text = $"收到动效事件：{context.AnimationKey}/{context.EventKey}。";
        }
    }
}
