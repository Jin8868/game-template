using System;
using System.Collections.Generic;
using AlloyFramework;
using AlloyFramework.UI;

namespace Game.UI
{
    public sealed class NavigationPopupUIController : UIController<NavigationPopupUIView>
    {
        private List<int> m_dataList; // 弹窗列表当前展示的数据。
        
        protected override void OnCreate()
        {
            View.BtnBack.onClick.AddListener(Back);
        }

        protected override void OnInitData(UIEmptyData data)
        {
            View.TxtStatus.text = "弹窗已打开，点击返回验证导航记录。";
            m_dataList = new List<int>(100);
            for (int i = 1; i < 100; i++)
            {
                m_dataList.Add(i);
            }

            // 首次打开只执行数据初始化，需要在此建立列表绑定。
            View.ListScrollView.BindData(m_dataList);
        }

        protected override void OnOpen()
        {
            // 首次打开不经过 OnRefresh，显示完成后单独触发定位。
            // 先提交初始 Canvas 布局，避免下一帧尺寸变化取消滚动动画。
            View.ListScrollView.ScrollToIndex(20, EUIListAlignment.Center, true, 0.5f);
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
