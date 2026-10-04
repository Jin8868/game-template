# Luban 配置工程

此目录是游戏配置源文件。执行 `../../Tools/Luban/Generate.ps1` 后，Luban 使用固定的
v5.1.0 CLI 生成 C# 代码与二进制配置。

`Defines/` 维护业务配置结构，`Datas/` 维护 Excel 数据。第一张表
`TbUIJumpConfig.xlsx` 的 `TargetUIName` 填写 `GameUI.g.cs` 生成的稳定 UI 名称，例如
`LoadingUI`。

生成输出不放在本目录：C# 输出至
`UnityProj/Assets/Scripts/Runtime/Generated/Config/`，数据输出至
`UnityProj/Assets/Res/Config/Luban/`。Luban 会清理 C# 输出目录，因此严禁在其中编写
手写代码。
