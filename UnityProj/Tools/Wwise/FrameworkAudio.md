# 框架音频说明

通用运行代码、Wwise 后端、音频编辑器工具与导出实现已迁至 AlloyFramework。

完整使用说明见 [AlloyFramework 音频文档](../../../../AlloyFramework/Documentation~/Audio.md)。
项目只维护 GameAudioInstaller 的资源约定、Collector 配置、启动预加载及业务 Event 调用。

当前 Init.bnk 已存在。AudioPackage 使用 RawFileIgnoreRule，允许收集 .bnk 和 .wem 文件；
请退出再进入 Play Mode，让 EditorSimulate 重新生成清单。
若运行 Offline/Host 模式，请重新构建并交付 AudioPackage。