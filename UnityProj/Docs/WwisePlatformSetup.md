# Wwise 平台插件与 iOS 构建准备

本项目使用普通 Git，不使用 Git LFS 保存 Wwise SDK。

## 固定版本

以 Assets/Wwise/Version.txt 为准。目前为：

- Wwise SDK：2025.1.11，Build 9262。
- Unity Integration Bundle：2025.1.11.4331。

升级时统一更新官方 Integration、原生库和音频构建产物，并同步本说明。

## 克隆后进行日常开发

Windows/Mac 编辑器插件和 Android 插件继续纳入 Git。iOS 静态库不用于 Windows/Mac 编辑器播放，也不用于 Android 构建。

仓库保留 iOS 的 C# 集成、头文件及配置，但忽略 iOS 插件目录内的所有 .a 和对应 .a.meta。因此，普通开发无需为了运行编辑器安装 iOS SDK；实际平台导入结果仍需由开发者验证。

## 构建 iOS 前

1. 在 iOS 构建机器上安装 Audiokinetic Launcher。
2. 安装与上述版本匹配的 Wwise，并选择需要的 iOS SDK 与插件。
3. 通过 Launcher 对此 Unity 项目执行官方修改/集成流程，补齐 iOS 库；不要手动升级为不同版本。
4. 根据目标选择 iphoneos 或 iphonesimulator，并使用所需的 Debug/Profile/Release 配置。
5. 核对 Assets/Wwise/API/Runtime/Plugins/iOS 内的目标静态库以及 Plugin Inspector 配置。
6. 为 iOS 生成对应平台的 SoundBank 和媒体资源，然后执行 Unity/Xcode 构建。

.a 的原始文件大小不等于最终安装包增加量，它作为链接输入使用。不要为减小 Git 仓库而直接裁剪或改写供应商静态库。

## Git 规则

忽略规则位于 UnityProj/.gitignore：

- /Assets/Wwise/API/Runtime/Plugins/iOS/**/*.a
- /Assets/Wwise/API/Runtime/Plugins/iOS/**/*.a.meta

本地安装的库保留在磁盘上，不上传普通 Git。新设备和 CI 必须按上述方式安装，不会因为 clone 自动取得这些库。

如果后续官方升级修改了已纳入 Git 的集成代码或配置，请审查这些变化；不要将安装操作产生的所有改动无条件提交。