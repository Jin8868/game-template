using System.Collections.Generic;
using AlloyFramework.UI;
using Game.Config;
using FrameworkBackMode = AlloyFramework.UI.EUIBackMode;
using FrameworkJumpConfig = AlloyFramework.UI.UIJumpConfig;
using FrameworkJumpMode = AlloyFramework.UI.EUIJumpMode;
using LubanBackMode = Game.Config.EUIBackMode;
using LubanJumpMode = Game.Config.EUIJumpMode;

namespace Game.UI
{
    internal sealed class LubanUIJumpConfigProvider : IUIJumpConfigProvider
    {
        private readonly Dictionary<int, FrameworkJumpConfig> m_configs; // 转换后的框架跳转配置。

        public LubanUIJumpConfigProvider(UIJumpConfigTable table)
        {
            m_configs = new Dictionary<int, FrameworkJumpConfig>(table.DataList.Count);
            foreach (var source in table.DataList)
            {
                var config = new FrameworkJumpConfig(
                    source.Id,
                    source.TargetUiName,
                    ConvertJumpMode(source.JumpMode),
                    ConvertBackMode(source.BackMode),
                    source.BackJumpId);
                m_configs.Add(config.JumpID, config);
            }
        }

        /// <summary>
        /// 尝试按跳转配置 ID 获取框架跳转配置。
        /// </summary>
        /// <param name="jumpID">跳转配置的唯一标识。</param>
        /// <param name="config">成功时返回框架跳转配置。</param>
        /// <returns>找到配置时返回 true，否则返回 false。</returns>
        public bool TryGet(int jumpID, out FrameworkJumpConfig config)
        {
            return m_configs.TryGetValue(jumpID, out config);
        }

        /// <summary>
        /// 获取全部框架跳转配置，用于启动阶段校验。
        /// </summary>
        /// <returns>全部框架跳转配置。</returns>
        public IEnumerable<FrameworkJumpConfig> GetAll()
        {
            return m_configs.Values;
        }

        private static FrameworkJumpMode ConvertJumpMode(LubanJumpMode jumpMode)
        {
            switch (jumpMode)
            {
                case LubanJumpMode.Overlay:
                    return FrameworkJumpMode.Overlay;
                case LubanJumpMode.Push:
                    return FrameworkJumpMode.Push;
                case LubanJumpMode.Replace:
                    return FrameworkJumpMode.Replace;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(jumpMode), jumpMode, null);
            }
        }

        private static FrameworkBackMode ConvertBackMode(LubanBackMode backMode)
        {
            switch (backMode)
            {
                case LubanBackMode.ReturnToSource:
                    return FrameworkBackMode.ReturnToSource;
                case LubanBackMode.InheritSource:
                    return FrameworkBackMode.InheritSource;
                case LubanBackMode.Explicit:
                    return FrameworkBackMode.Explicit;
                case LubanBackMode.Disabled:
                    return FrameworkBackMode.Disabled;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(backMode), backMode, null);
            }
        }
    }
}
