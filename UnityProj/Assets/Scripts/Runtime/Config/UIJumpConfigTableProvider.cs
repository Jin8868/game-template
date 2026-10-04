using AlloyFramework;
using Game.Config;

namespace Game
{
    internal sealed class UIJumpConfigTableProvider : IAlloyConfigTable<UIJumpConfig>
    {
        private readonly UIJumpConfigTable m_table; // Luban 生成的 UI 跳转配置表。

        public UIJumpConfigTableProvider(UIJumpConfigTable table)
        {
            m_table = table;
        }

        /// <summary>
        /// 尝试按配置 ID 获取 UI 跳转配置。
        /// </summary>
        /// <param name="configID">UI 跳转配置的唯一标识。</param>
        /// <param name="config">成功时返回 UI 跳转配置。</param>
        /// <returns>找到配置记录时返回 true，否则返回 false。</returns>
        public bool TryGet(int configID, out UIJumpConfig config)
        {
            config = m_table.GetOrDefault(configID);
            return config != null;
        }
    }
}
