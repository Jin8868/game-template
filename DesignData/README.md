# Design Data

此目录是配置表、协议定义及其导出工具的仓库级唯一来源。源文件不放入 Unity 的
`Assets`，避免 Excel、定义文件和工具被 Unity 导入或由 YooAsset 收录。

## Luban 目录约定

```text
game-template/
├─ Tools/
│  └─ Luban/                         # 固定版本的 CLI、许可证和生成脚本
├─ DesignData/
│  └─ Luban/
│     ├─ Defines/                    # bean、enum、table 等 Luban 定义
│     ├─ Datas/                      # Excel 源表
│     ├─ Templates/                  # 项目维护的生成模板（需要时）
│     └─ README.md                   # 字段、生成和校验规则
└─ UnityProj/
   └─ Assets/
      ├─ Scripts/Runtime/Generated/Config/
      │  └─ ...                      # 生成的 C#，由 Game.Runtime 编译
      └─ Res/Config/Luban/
         └─ ...                      # 生成的二进制配置，由 YooAsset 收录
```

`Assets/Res/Config` 已是 YooAsset 的 `config` 收集组。配置加载器应通过
`Config/Luban/...` 地址加载生成数据；不得让业务代码直接读取 Excel 或二进制文件。

第一张表为 `UIJumpConfig`：定义文件放在
`DesignData/Luban/Defines/`，Excel 放在 `DesignData/Luban/Datas/`。其中
`TargetUIName` 使用 UI 生成代码中的稳定名称，例如 `GameUI.LoadingUI` 对应的
`LoadingUI`，不使用预制体路径或额外的数字 UI ID。

生成脚本必须使用 `Tools/Luban` 中锁定版本的 CLI，生成 C# 和二进制数据，并在
定义、主键或枚举校验失败时返回非零退出码。脚本不得修改 AlloyFramework 文件。
