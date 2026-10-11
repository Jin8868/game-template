using System;
using System.Reflection;
using System.Threading;
using AlloyFramework;
using Cysharp.Threading.Tasks;

namespace Game
{
    internal sealed class DefaultGameCodePreparation : IGameCodePreparation
    {
        private readonly GameHotUpdateSettings m_settings; // 静态业务程序集和显式安装入口配置。

        internal DefaultGameCodePreparation(GameHotUpdateSettings settings)
        {
            m_settings = settings;
        }

        /// <summary>从静态业务程序集中安装配置加载器。</summary>
        /// <param name="context">当前框架上下文。</param>
        /// <param name="cancellationToken">启动取消令牌。</param>
        /// <returns>业务安装完成的操作。</returns>
        /// <exception cref="InvalidOperationException">静态业务程序集或安装入口缺失。</exception>
        public UniTask PrepareAsync(FrameworkContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Default 模式只使用主包静态代码；不读取或加载热更新 DLL。
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != m_settings.RuntimeAssemblyName)
                {
                    continue;
                }
                MethodInfo installer = assembly.GetType(m_settings.InstallerTypeName, false)?.GetMethod(
                    "Install", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (installer == null || installer.ReturnType != typeof(void) || installer.ContainsGenericParameters)
                {
                    throw new InvalidOperationException("Default 模式业务安装入口缺失或签名不合法。");
                }
                installer.Invoke(null, null);
                return UniTask.CompletedTask;
            }
            throw new InvalidOperationException("Default 模式需要将业务程序集静态编译到主包。");
        }
    }
}
