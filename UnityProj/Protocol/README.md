# 项目网络接入流程

模板只保留网络初始化与协议生成配置，不附带测试会话、地址、账号、背包协议或业务实现。

启动顺序：GameEntry.MainAsync → GameStartupPipeline → InitializeGameNetworkStep。
GameNetworkRuntime 安装空的 NetworkServiceRegistry；管线退出时解除注册表并释放服务。
框架 NetworkManager 管理会话，注册表按类型获取服务；模板启动时不创建或连接服务器会话。

项目接入实际网络时：

1. 在 Protocol/Proto 添加真实协议，通过网络生成菜单生成消息和分部 Service。
2. 配置项目的传输、协议与会话，在项目初始化中调用生成的 GameNetworkServices.Register。
3. 在手写 Service 回调中实现业务处理，生成文件不写业务逻辑。
4. 调用 NetworkManager.Instance.GetService<TService>() 获取服务并发包。

NetworkGeneration.json 保留源目录、输出目录、工具路径与程序集设置，当前 SelectedFiles 为空。
首次生成后将 Game.Protocol 与 Alloy.Net.Protobuf 加入 Game.Runtime 程序集引用，
并配置实际构建需要的 IL2CPP 类型保留。生成清单与协议输出需随实际项目提交。
框架生成器会保留已有手写回调，并在 AlloyNetworkHandlers 标记前追加缺失处理入口。

本机 Tools/NetworkDemoServer 仅在本地保留并由 Git 忽略，不属于模板流程。
