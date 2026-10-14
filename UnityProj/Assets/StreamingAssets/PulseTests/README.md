# Pulse 本地测试素材

Synthetic、Alpha、Sintel 分别为帧号测试、透明测试和真实电影。原片和生成物保留在 PulseVideoPlayer/Tests；此处仅放运行时 .pulse 与独立音轨。素材及其 Unity meta 不提交 Git。

Sintel：Blender Foundation / Durian Open Movie Project，2010，CC BY 3.0。来源：https://durian.blender.org/download/

测试入口：★AlloyFramework★/视频/创建或打开首帧测试场景，然后按 Unity Play（F5 会启动框架）。
测试场景仅在编辑器独立运行时跳过框架启动；不加入默认正式构建场景。
按钮、内存采样和资源日志仅在 UNITY_EDITOR 或 DEVELOPMENT_BUILD 下启用。
当前播放器提供 Windows x64 / D3D11 连续播放、暂停/恢复和 Seek；基础连续播放已验证，Seek 与预览 UI 仍需用户运行验收。音频计划通过 Wwise 适配，尚未接入。
使用 ABI 3 DLL，再选择“创建或打开播放测试场景”；旧首帧菜单也可用。原生源码变更后关闭 Unity 并运行播放器 Tools/BuildWindowsNative.bat 重建。
已登记线程句柄在播放和暂停时可为 1，结束后保留末帧纹理；关闭后才应全部归零。
Git 忽略只控制提交；本地 StreamingAssets 中的测试素材仍可能被 Unity 构建收集。
正式发布前应移走此目录里的本地测试素材，保留原片在 PulseVideoPlayer/Tests。
