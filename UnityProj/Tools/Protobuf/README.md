# Protobuf 工具

本项目锁定 Google.Protobuf 3.34.1 与 protoc 34.1。
`Windows/34.1/bin/protoc.exe` 用于实际生成；include 仅保留自定义 option 所需的
`google/protobuf/descriptor.proto`，已删除当前未使用的标准协议文件。
如果以后 import 其他标准协议，需要从同版本官方工具包补回对应文件及其依赖。

来源：[官方 v34.1 发布](https://github.com/protocolbuffers/protobuf/releases/tag/v34.1)。
归档与工具 SHA-256 记录在 `Versions.json`，许可证见 `LICENSE.txt`。
Mac/Linux 开发需要取得同版本官方工具并调整项目相对配置。

Unity 菜单：`★AlloyFramework★/网络/协议代码生成`。
配置位于 `Protocol/NetworkGeneration.json`，协议源位于 `Protocol/Proto`。
官方消息、分部 Service 收发代码和注册表位于 `Assets/Scripts/Protocol/Generated`。
手写业务 Service 位于 `BusinessServiceRoot`，生成器保留已有逻辑并追加缺失回包骨架。
`ServiceAssembly` 必须与手写 partial 所在程序集一致。详细说明见 `Protocol/README.md`。

框架 option 定义位于 `Packages/com.alloy.framework/Editor/Network/Protocol/AlloyNetworkOptions.proto`。
修改 option 时同时重新生成框架 `Runtime/NetProtobuf/Options/AlloyNetworkOptions.cs`，
并补选全部受影响业务协议。服务端通过同级框架仓库取得同版定义。

所有协议修改从 proto 和维护中的生成器开始，不手动编辑 `.g.cs` 或官方消息输出。
清单会检测生成文件的手工修改并停止覆盖。生成器版本为 2。
