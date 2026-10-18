# Unity Logs Viewer

从本机 HZJFrameWork 项目复制 Reporter、ReporterMessageReceiver、MultiKeyDictionary 及 Images 资源。
来源项目路径：../../HZJFrameWork/Assets/HZJFrameWork（相对于 Unity 工程根目录）。
原插件：https://github.com/aliessmael/Unity-Logs-Viewer。

此本地版本曾被旧项目改为 ReporterRoot 窗口。本次移除了该依赖，恢复画圈唤出及跨场景保留，
销毁时解绑日志事件。未引入旧项目自动重写 build_info 文件的编辑器脚本。

Game.Bootstrap 在框架启动前实例化 Resources/GameLogViewer.prefab，日志面板使用 IMGUI，
不依赖 DefaultPackage、业务 UI 或热更新程序集。右上角“日志”按钮与 F8 均可唤出，原画圈入口仍保留。
对象在 BeforeSceneLoad 创建，Boot 场景提供幂等安装回退；启动日志会输出安装状态。
Android 模拟器可能拦截电脑功能键，应同时检查右上角按钮及安装日志。
图标和皮肤的 .meta 保留自来源项目。插件保持 AOT，在所有构建中可用。

新增或修改本插件的 AOT 脚本后，需要重新准备主包代码基线并重新构建 Player。
