# 公开Core编译依赖静态审计

审计范围仅`Assets/OSSASTRA/Scripts/GroundResearch`。未读取/递归`Assets/Script`、`Assets/Scripts`，未启动Unity或另建工程。当前目录31个C#，排除下述6文件后保留25个C#及2个自有Shader。

## 结论与准确排除名

当前保留Core中没有发现对排除类型、角色、敌人、MapResearch、SpineDepth或Funplay的C#类型引用。静态检查不能替代干净工程的实际编译。

实际旧导入器文件名是`Editor/GroundSourceImporter.cs`，不是`FrontierGroundImporter.cs`。导出白名单应排除真实文件名，不能只排除不存在的旧称。

排除：

- Editor/GroundSourceImporter.cs
- Editor/FrontierGroundSceneBuilder.cs
- Editor/FrontierGroundBrushWindow.cs
- Runtime/GroundResearchView.cs
- Editor/GroundWorkshopPlaygroundBuilder.cs
- Editor/GroundWorkshopPlaygroundSession.cs

## 关键接缝

| 当前位置 | 发现 | 最窄处理 |
| --- | --- | --- |
| Editor/FrontierGroundBrush.cs:53–55 | 受限画笔只依赖保留的GroundWorkshopMap、Ownership与Authoring；当前没有`FrontierGroundSceneBuilder.ScenePath`引用 | 保留同一文件中的Brush和BrushEditor，不必再做源码替换或复制第二套画笔 |
| Editor/FrontierGroundBrushWindow.cs:63/69/114/116/158/189 | 旧窗口直接依赖旧SceneBuilder；69行含其ScenePath | 整文件排除。新窗口已取代其工坊功能 |
| Editor/GroundWorkshopPlaygroundBuilder.cs:49、GroundWorkshopPlaygroundSession.cs:60 | 可选双角色模块依赖保护旧场的ScenePath以及项目玩法类型 | 两文件整体排除；保留Core PlayHooks让纯插件正常显示不可用状态 |
| Editor/GroundWorkshopCatalog.cs:47、214–220 | LocalGenerated路径是本机原生族引用白名单字符串；使用的是Unity Tile/RuleTile，没有引用旧导入器类型 | 可保留目录服务代码；不打包LocalGenerated、源图鉴与媒体。独立安装依靠原创族，不应伪造该本机参考资源存在 |
| Editor/GroundWorkshopCatalog.cs:17–18 | 使用Newtonsoft.Json/JObject | 在包依赖/说明中明确Newtonsoft，不能依赖本工程其他模块碰巧引入 |
| Runtime/GroundDecorationOrientation.cs:11–13、58–60 | Runtime文件使用UnityEditor/SceneView，但两处均受UNITY_EDITOR条件保护 | 保持条件与Runtime/Editor目录结构，不移成无条件运行时引用 |
| Shaders/GroundMaskWorld.shader:22、GroundDecorationCutout.shader:20 | 依赖`Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl` | 明确URP依赖；不声明Built-in/HDRP可直接使用 |

保留的Surface/Layer与范围计算无项目玩法依赖；Bounds/BoundsInt是Unity类型，不存在必须另包一份的自有Bounds源码。

## 必须随Core保留

- 根目录GroundAuthorTile.cs、GroundAuthorRuleTile.cs。
- Runtime除GroundResearchView外全部11文件（Surface/Layer、Map/Registration、Recipe/Family/Theme、装饰定义/实例/朝向、契约）。
- Editor：FrontierGroundBrush.cs（含BrushEditor）、GroundTilemapAttributes.cs、GroundAssetLedger.cs，以及所有GroundWorkshop*.cs中除PlaygroundBuilder/Session之外的文件。
- Shaders/GroundMaskWorld.shader、GroundDecorationCutout.shader。

不要把“名字带Frontier”作为导出删除条件，否则会漏掉新工坊真实使用的Native画笔。不要把“Runtime里出现UnityEditor”直接视为错误而删除整个Orientation，需保留其条件编译区。

## 外部依赖与验证边界

25文件依赖UnityEngine/Tilemap、UnityEditor/SceneManagement/EditorTools/Tilemaps，RuleTile与GridBrush/GridBrushEditor及Tile Palette实现，以及Newtonsoft.Json；两Shader依赖URP。root随后已实际运行Native API探针：RuleTile由`Unity.2D.Tilemap.Extras.dll`提供，GridBrush/GridBrushEditor由`Unity.2D.Tilemap.Editor.dll`提供，JObject由`com.unity.nuget.newtonsoft-json@3.2.2/Runtime/Newtonsoft.Json.dll`提供。探针未确认Tilemap包版本，不据此扩张宣称其他版本均可用。

该Native探针也确认AssemblyBuilder默认引用包含大量本工程玩法/角色与第三方DLL，因此普通项目内编译不能充当独立证明。新candidate明确只留Editor安装目录的引擎/NetStandard DLL与上述三个包提供者，排除其他所有默认引用；只有实际异步Build结束的CompilerMessage与DLL回执可证明这轮独立Editor源编译。此处仍不冒称Player/空工程导入已验证。

如果新增asmdef，Editor程序集需引用自有Runtime、Tilemap编辑器/Extras与Newtonsoft；Runtime程序集需能解析RuleTile。请以实际安装包的asmdef名称为准，不用猜测字符串。Editor文件夹必须保留，避免UnityEditor类型进入Player程序集。

建议发布前执行两步独立验证：先对明确25源文件+2 Shader做白名单导出；再在只安装声明依赖的空工程打开Core、生成原创三层地图、Native Palette落笔/撤销、保存重开。当前Native项目内编译与功能通过不能代替该项。
