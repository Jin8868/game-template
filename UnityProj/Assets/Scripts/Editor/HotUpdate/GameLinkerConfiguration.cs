using System.IO;
using System.Text;
using System.Xml;
using AlloyFramework;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Game.Editor
{
    /// <summary>在裁剪前将自动依赖与入口规则写入项目唯一的裁剪配置。</summary>
    public sealed class GameLinkerConfiguration : IPreprocessBuildWithReport
    {
        internal const string GENERATEDPATH = "../Library/HybridCLR/LinkerDependencies.xml"; // 相对 Assets 的中间文件。
        private const string LINKPATH = "Assets/link.xml"; // 项目唯一生效的裁剪配置。
        // 标记之后的节点全部由生成流程维护。
        private const string GENERATEDMARKER = " 自动生成规则：由 GameLinkerConfiguration 维护，请勿手动修改。 ";

        /// <summary>在业务基线校验和 HybridCLR 临时裁剪构建前合并规则。</summary>
        public int callbackOrder => -10001;

        /// <summary>构建前更新统一裁剪配置，包括 HybridCLR 的临时 AOT 构建。</summary>
        /// <param name="report">当前构建报告。</param>
        /// <exception cref="BuildFailedException">裁剪配置根节点无效或缺少热更新裁剪依赖。</exception>
        public void OnPreprocessBuild(BuildReport report)
        {
            Synchronize(GameHotUpdateSettings.Load());
        }

        internal static void Synchronize(GameHotUpdateSettings settings)
        {
            // 标记之前的规则由项目维护，之后的规则每次重新生成，避免切换模式后残留。
            var document = new XmlDocument();
            document.Load(LINKPATH);
            XmlElement root = document.DocumentElement;
            if (root == null || root.Name != "linker")
            {
                throw new BuildFailedException("Assets/link.xml 缺少 linker 根节点。");
            }

            bool generated = false;
            foreach (XmlNode node in root.SelectNodes("node()"))
            {
                if (node is XmlComment && node.Value == GENERATEDMARKER) generated = true;
                if (generated) root.RemoveChild(node);
            }

            root.AppendChild(document.CreateComment(GENERATEDMARKER));
            if (settings.CodeMode == EGameCodeMode.Default)
            {
                // 静态业务入口由反射创建，Default 模式保留整个业务程序集。
                XmlElement assembly = document.CreateElement("assembly");
                assembly.SetAttribute("fullname", settings.RuntimeAssemblyName);
                assembly.SetAttribute("preserve", "all");
                root.AppendChild(assembly);
            }
            else
            {
                // 官方工具只写中间文件，避免覆盖项目手工维护的固定规则。
                string generatedPath = Path.GetFullPath(Path.Combine("Assets", GENERATEDPATH));
                if (!File.Exists(generatedPath))
                {
                    throw new BuildFailedException("缺少 HybridCLR 裁剪依赖，请先执行主包代码准备。");
                }

                var dependencies = new XmlDocument();
                dependencies.Load(generatedPath);
                foreach (XmlNode assembly in dependencies.SelectNodes("/linker/assembly"))
                {
                    // 固定规则已经完整保留的程序集不重复写入自动依赖。
                    bool preserved = false;
                    foreach (XmlElement existing in root.SelectNodes("assembly"))
                    {
                        if (existing.GetAttribute("fullname") == assembly.Attributes["fullname"].Value &&
                            existing.GetAttribute("preserve") == "all")
                        {
                            preserved = true;
                            break;
                        }
                    }

                    if (!preserved) root.AppendChild(document.ImportNode(assembly, true));
                }
            }

            // 内容未变化时不触碰文件，避免无意义的资源导入和构建指纹变化。
            using (var stream = new MemoryStream())
            {
                var writerSettings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
                using (XmlWriter writer = XmlWriter.Create(stream, writerSettings)) document.Save(writer);
                string content = Encoding.UTF8.GetString(stream.ToArray());
                if (File.ReadAllText(LINKPATH) != content)
                {
                    File.WriteAllText(LINKPATH, content, new UTF8Encoding(false));
                }
            }
        }
    }
}
