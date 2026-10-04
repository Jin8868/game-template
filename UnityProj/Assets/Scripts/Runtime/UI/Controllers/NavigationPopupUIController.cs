using System;
using AlloyFramework;
using AlloyFramework.UI;

namespace Game.UI
{
    public sealed class NavigationPopupUIController : UIController<NavigationPopupUIView>
    {
        protected override void OnCreate()
        {
            View.BtnBack.onClick.AddListener(Back);
        }

        protected override void OnInitData(UIEmptyData data)
        {
            View.TxtStatus.text = "弹窗已打开，点击返回验证导航记录。";
        }

        protected override void OnRefresh(UIEmptyData data)
        {
            View.TxtStatus.text = "弹窗已刷新。";
        }

        protected override void OnDispose()
        {
            View.BtnBack.onClick.RemoveListener(Back);
        }

        private void Back()
        {
            View.TxtStatus.text = "正在执行返回……";
            UIManager.Instance.Back(LogBackCompleted);
        }

        private static void LogBackCompleted(bool hasReturned, Exception exception)
        {
            if (exception != null)
            {
                AlloyDebug.Error(exception);
                return;
            }

            AlloyDebug.Log($"[NavigationValidation] 弹窗返回结果：{hasReturned}");
        }
    }
}
