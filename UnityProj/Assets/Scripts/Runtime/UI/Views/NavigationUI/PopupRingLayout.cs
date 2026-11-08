using System;
using System.Collections.Generic;
using AlloyFramework.UI;
using UnityEngine;

namespace Game.UI
{
    /// <summary>导航弹窗的圆环布局，以单轴拖动距离驱动 Item 沿圆周运动。</summary>
    public sealed class PopupRingLayout : UIListLayoutBase
    {
        private readonly int m_visibleCount = 8; // 一圈最多显示的项数。
        private readonly float m_radius = 160f; // 期望半径，实际受视口限制。
        private readonly float m_scrollStep = 100f; // 每移动一项对应的逻辑距离。
        private readonly float m_focusAngle = -90f; // 居中项的角度，默认位于圆环顶部。
        private readonly float m_focusScale = 1.1f; // 居中项缩放。
        private readonly float m_backScale = 0.85f; // 对侧项缩放。

        private int m_count; // 当前数据数量。
        private int m_slots; // 本轮使用的环形槽位数。
        private float m_step; // 本轮使用的逻辑步长。
        private float m_actualRadius; // 适配视口后的圆环半径。
        private Vector2 m_center; // 视口内圆心，向右下为正。
        private Vector2 m_itemSize; // 当前 Item 的布局尺寸。
        private Vector2 m_contentSize; // 提供给 ScrollRect 的逻辑尺寸。

        /// <summary>创建独立的圆环布局，不需要挂载 GameObject。</summary>
        /// <param name="radius">期望圆环半径。</param>
        /// <param name="visibleCount">一圈最多显示的项数，至少三项。</param>
        /// <param name="scrollStep">每项对应的逻辑滚动距离。</param>
        /// <param name="focusAngle">定位焦点角度，负九十度表示顶部。</param>
        /// <param name="focusScale">焦点项缩放。</param>
        /// <param name="backScale">对侧项缩放。</param>
        /// <exception cref="ArgumentOutOfRangeException">参数不是有限数或超出允许范围。</exception>
        public PopupRingLayout(
            float radius = 160f,
            int visibleCount = 8,
            float scrollStep = 100f,
            float focusAngle = -90f,
            float focusScale = 1.1f,
            float backScale = 0.85f)
        {
            // 构造时校验，避免非法配置在滚动期间产生无效坐标。
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (visibleCount < 3) throw new ArgumentOutOfRangeException(nameof(visibleCount));
            if (float.IsNaN(scrollStep) || float.IsInfinity(scrollStep) || scrollStep < 1f)
                throw new ArgumentOutOfRangeException(nameof(scrollStep));
            if (float.IsNaN(focusAngle) || float.IsInfinity(focusAngle))
                throw new ArgumentOutOfRangeException(nameof(focusAngle));
            if (float.IsNaN(focusScale) || float.IsInfinity(focusScale) || focusScale < 0.1f)
                throw new ArgumentOutOfRangeException(nameof(focusScale));
            if (float.IsNaN(backScale) || float.IsInfinity(backScale) || backScale < 0.1f)
                throw new ArgumentOutOfRangeException(nameof(backScale));
            m_radius = radius;
            m_visibleCount = visibleCount;
            m_scrollStep = scrollStep;
            m_focusAngle = focusAngle;
            m_focusScale = focusScale;
            m_backScale = backScale;
        }

        /// <summary>滚动时更新仍可见 Item 的位置和缩放。</summary>
        public override bool ChangesWithScroll => true;

        /// <summary>使用视口坐标，框架负责抵消 Content 的平移。</summary>
        public override bool UsesViewportCoordinates => true;

        /// <summary>缓存圆心、半径及逻辑滚动范围。</summary>
        /// <param name="context">当前数据和视口配置。</param>
        public override void Rebuild(UIListLayoutContext context)
        {
            m_count = context.Items.Count;
            m_slots = Mathf.Max(3, m_visibleCount);
            m_step = Mathf.Max(1f, m_scrollStep);
            m_itemSize = context.DefaultItemSize;

            // 用最大缩放后的占用尺寸约束半径，使整圈 Item 留在裁剪范围内。
            float width = Mathf.Max(0f, context.ViewportSize.x - context.Padding.horizontal);
            float height = Mathf.Max(0f, context.ViewportSize.y - context.Padding.vertical);
            m_center = new Vector2(context.Padding.left + width * 0.5f, context.Padding.top + height * 0.5f);
            float largestScale = Mathf.Max(0.1f, Mathf.Max(m_focusScale, m_backScale));
            float availableRadius = Mathf.Min(
                (width - m_itemSize.x * largestScale) * 0.5f,
                (height - m_itemSize.y * largestScale) * 0.5f);
            m_actualRadius = Mathf.Clamp(m_radius, 0f, Mathf.Max(0f, availableRadius));

            // 最后一条也能移动到顶部定位点，内容尺寸不使用圆环的视觉包围盒。
            float travel = Mathf.Max(0, m_count - 1) * m_step;
            m_contentSize = context.ViewportSize;
            if (context.Vertical) m_contentSize.y += travel;
            else m_contentSize.x += travel;
        }

        /// <summary>返回逻辑内容尺寸。</summary>
        /// <returns>视口尺寸加上主轴行程。</returns>
        public override Vector2 GetContentSize()
        {
            return m_contentSize;
        }

        /// <summary>查询一圈内的数据索引，在圆环对侧替换离开的项。</summary>
        /// <param name="start">普通列表的缓冲起点，圆环使用自己的角度窗口。</param>
        /// <param name="end">普通列表的缓冲终点，圆环使用自己的角度窗口。</param>
        /// <param name="scrollPosition">当前逻辑距离。</param>
        /// <param name="results">已清空的输出集合。</param>
        public override void CollectVisibleItems(
            float start, float end, float scrollPosition, List<int> results)
        {
            // 半开角度窗口保证每个位置最多一个 Item，不重复显示相同数据。
            float centerIndex = scrollPosition / m_step;
            int first = Mathf.FloorToInt(centerIndex - m_slots * 0.5f) + 1;
            int last = Mathf.Min(m_count - 1, first + m_slots - 1);
            first = Mathf.Max(0, first);
            for (int index = first; index <= last; index++) results.Add(index);
        }

        /// <summary>将数据相对定位点的距离映射到圆周位置。</summary>
        /// <param name="index">有效数据索引。</param>
        /// <param name="scrollPosition">当前逻辑距离。</param>
        /// <returns>视口坐标下的位置和缩放，文字不随圆周旋转。</returns>
        public override UIListItemLayout GetItemLayout(int index, float scrollPosition)
        {
            float relativeIndex = index - scrollPosition / m_step;
            float relativeAngle = relativeIndex * Mathf.PI * 2f / m_slots;
            float angle = m_focusAngle * Mathf.Deg2Rad + relativeAngle;
            float weight = (Mathf.Cos(relativeAngle) + 1f) * 0.5f;
            float scale = Mathf.Lerp(Mathf.Max(0.1f, m_backScale), Mathf.Max(0.1f, m_focusScale), weight);
            Vector2 center = m_center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * m_actualRadius;

            // 框架采用左上 pivot，需要按缩放后半尺寸抵消偏移，让 Item 中心位于圆周。
            var layout = new UIListItemLayout(new Rect(center - m_itemSize * (scale * 0.5f), m_itemSize));
            layout.Scale = Vector2.one * scale;
            return layout;
        }

        /// <summary>计算定位目标，Center 对应顶部焦点，Start 和 End 对应角度窗口两端。</summary>
        /// <param name="index">目标数据索引。</param>
        /// <param name="alignment">路径窗口对齐方式。</param>
        /// <param name="currentPosition">当前逻辑距离。</param>
        /// <returns>目标滚动距离。</returns>
        /// <exception cref="ArgumentOutOfRangeException">目标索引无效。</exception>
        public override float GetScrollPosition(
            int index, EUIListAlignment alignment, float currentPosition)
        {
            if (index < 0 || index >= m_count) throw new ArgumentOutOfRangeException(nameof(index));
            float offset = m_slots * 0.5f - 0.5f;
            if (alignment == EUIListAlignment.Start) return (index + offset) * m_step;
            if (alignment == EUIListAlignment.End) return (index - offset) * m_step;
            return index * m_step;
        }

        /// <summary>释放本轮数据相关的布局状态。</summary>
        public override void Clear()
        {
            base.Clear();
            m_count = 0;
            m_contentSize = Vector2.zero;
        }
    }
}
