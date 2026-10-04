using System;
using AlloyFramework;
using AlloyFramework.UI;

namespace Game.UI
{
    public sealed class NavigationHomeUIController : UIController<NavigationHomeUIView>
    {
        protected override void OnCreate()
        {
            View.BtnOpenDetail.onClick.AddListener(OpenDetail);
            View.BtnOpenPopup.onClick.AddListener(OpenPopup);
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
            View.BtnOpenDetail.onClick.RemoveListener(OpenDetail);
            View.BtnOpenPopup.onClick.RemoveListener(OpenPopup);
        }

        private void OpenDetail()
        {
            View.TxtStatus.text = "正在以 Push 模式打开详情页……";
            UIManager.Instance.Jump(
                NavigationJumpID.DETAIL,
                onCompleted: OnJumpCompleted);
        }

        private void OpenPopup()
        {
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
    }
}
