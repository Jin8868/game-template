using System.Collections;
using System.Collections.Generic;
using AlloyFramework.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public class PopupItem : UIListItem
    {
        private Text m_txtTitle; // 当前列表项的标题文本。

        protected override void OnInitialize()
        {
            base.OnInitialize();
            // 列表在未激活状态下初始化实例，查找时必须包含未激活的子节点。
            m_txtTitle = GetComponentInChildren<Text>(true);
        }

        protected override void OnBind()
        {
            if (Data is int index)
            {
                if (m_txtTitle != null)
                {
                    m_txtTitle.text = index.ToString();
                }
            }
        }
    }
}
