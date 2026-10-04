using System;
using AlloyFramework;
using AlloyFramework.UI;

namespace Game.UI
{
    public sealed class NavigationDetailUIController : UIController<NavigationDetailUIView>
    {
        protected override void OnCreate()
        {
            View.BtnBack.onClick.AddListener(Back);
            View.BtnOpenDetail.onClick.AddListener(OpenPopup);
            View.BtnSkipBack.onClick.AddListener(OpenSkipBackPopup);
        }

        protected override void OnInitData(UIEmptyData data)
        {
            View.TxtStatus.text = "详情页已打开，首页应处于暂停状态。";
        }

        protected override void OnRefresh(UIEmptyData data)
        {
            View.TxtStatus.text = "详情页已刷新。";
        }

        protected override void OnPause()
        {
            View.TxtStatus.text = "详情页已暂停。";
        }

        protected override void OnResume()
        {
            View.TxtStatus.text = "详情页已恢复。";
        }

        protected override void OnDispose()
        {
            View.BtnBack.onClick.RemoveListener(Back);
            View.BtnOpenDetail.onClick.RemoveListener(OpenPopup);
            View.BtnSkipBack.onClick.RemoveListener(OpenSkipBackPopup);
        }

        private void Back()
        {
            View.TxtStatus.text = "正在返回首页……";
            UIManager.Instance.Back(LogBackCompleted);
        }

        private void OpenPopup()
        {
            View.TxtStatus.text = "正在打开普通返回弹窗……";
            UIManager.Instance.Jump(
                NavigationJumpID.POPUP,
                onCompleted: OnJumpCompleted);
        }

        private void OpenSkipBackPopup()
        {
            View.TxtStatus.text = "正在打开跨层返回弹窗……";
            UIManager.Instance.Jump(
                NavigationJumpID.SKIPBACK,
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

        private static void LogBackCompleted(bool hasReturned, Exception exception)
        {
            if (exception != null)
            {
                AlloyDebug.Error(exception);
                return;
            }

            AlloyDebug.Log($"[NavigationValidation] 返回结果：{hasReturned}");
        }
    }
}
