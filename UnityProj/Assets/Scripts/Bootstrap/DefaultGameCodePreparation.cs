using System;
using System.Reflection;
using System.Threading;
using AlloyFramework;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Game
{
    internal sealed class DefaultGameCodePreparation : IGameCodePreparation
    {
        private readonly GameCodePreparationResult m_result; // 供后续步骤读取的静态业务代码准备结果。
        private readonly string m_runtimeAssemblyName; // 静态业务程序集名称。
        private readonly string m_installerTypeName; // 显式业务安装入口类型名称。

        /// <summary>根据主包启动配置创建静态业务代码准备服务。</summary>
        /// <param name="result">与后续业务步骤共享的准备结果。</param>
        /// <param name="runtimeAssemblyName">静态业务程序集名称。</param>
        /// <param name="installerTypeName">业务安装入口的完整类型名称。</param>
        /// <exception cref="ArgumentNullException">参数为空。</exception>
        [Preserve]
        public DefaultGameCodePreparation(
            GameCodePreparationResult result,
            string runtimeAssemblyName,
            string installerTypeName)
        {
            m_result = result ?? throw new ArgumentNullException(nameof(result));
            m_runtimeAssemblyName = runtimeAssemblyName ?? throw new ArgumentNullException(nameof(runtimeAssemblyName));
            m_installerTypeName = installerTypeName ?? throw new ArgumentNullException(nameof(installerTypeName));
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
                if (assembly.GetName().Name != m_runtimeAssemblyName)
                {
                    continue;
                }
                MethodInfo installer = assembly.GetType(m_installerTypeName, false)?.GetMethod(
                    "Install", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (installer == null || installer.ReturnType != typeof(void) || installer.ContainsGenericParameters)
                {
                    throw new InvalidOperationException("Default 模式业务安装入口缺失或签名不合法。");
                }
                installer.Invoke(null, null);
                cancellationToken.ThrowIfCancellationRequested();
                m_result.RuntimeAssembly = assembly;
                m_result.IsCompleted = true;
                return UniTask.CompletedTask;
            }
            throw new InvalidOperationException("Default 模式需要将业务程序集静态编译到主包。");
        }
    }
}
