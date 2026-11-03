# 旧验证场景清理审计

日期：2026-11-03。任务：RENDER-INTEGRATE-01。审计者：cleanup_verify；只读检查，未操作 Unity、未删除资产、未改 BuildSettings。

## 结论与执行门

首批 17 场均为历史安全副本或已被新验证主场替代的私有检查点，可作为清理候选。当前允许目录内的场景名、完整路径、动态命名后缀和备份目录检索，未发现运行代码或序列化资产的显式使用；8 处匹配全部是历史说明。尚未执行 Native 反向依赖核验，因此本文不把“文本未命中”写成“无引用已验证”。

Root 在删除前须逐个完成：

1. 从 `AssetDatabase.AssetPathToGUID` 取得真实 GUID，并用 GUIDToAssetPath 回读；团结的 `.meta` 内容不能按普通十六进制 GUID 文本猜测。
2. Native 读取当前 EditorBuildSettings 与已打开场景，任何候选出现在其中都先保留；包括禁用的构建项也不直接破坏。
3. 对候选外的项目序列化资产调用 `AssetDatabase.GetDependencies` 检查引用，排除禁止检查的 `Assets/Script/` 和 `Assets/Scripts/`。至少覆盖 `.unity`、`.prefab`、`.asset`、`.controller`、`.overrideController`、`.playable`。以 Native 路径结果而非磁盘 meta 字符串作为依赖依据。
4. 在允许目录再检索 Native GUID，覆盖按字符串存储的 Addressables/场景索引；结合本文的名称与动态命名片段检索。任一活动引用则移出删除清单，不静默改旧业务或登记表。
5. 将每个 `.unity` 和对应 `.meta` 逐个复制到 `Assets` 外的本轮归档目录，记录源/副本长度和 SHA256，并校验完全相同。历史资料提到的旧副本可以由该归档恢复，不需要留在 Unity 场景列表里。
6. 仅对通过门禁的精确场景路径调用 `AssetDatabase.DeleteAsset`；不用目录递归删除，不删引用到的材质、模型、配置、脚本等共享资源。删除后再次核对候选资产不存在、保留场和构建项仍能解析，并由 Root 运行主场检查。

## 精确候选清单

| 场景路径 | 场景字节数 | 类别 |
|---|---:|---|
| Assets/Scenes/XingHaiHuiLang/Backups/SC_Cartoon_BeforeAnimeV2.unity | 82,633 | 历史安全副本 |
| Assets/Scenes/XingHaiHuiLang/Backups/SC_Cartoon_BeforeDecay.unity | 87,362 | 历史安全副本 |
| Assets/Scenes/XingHaiHuiLang/Backups/SC_Cartoon_BeforePaint_20261005.unity | 81,701 | 历史安全副本 |
| Assets/Scenes/XingHaiHuiLang/Backups/SC_DarkDungeon_BeforeRenderLab.unity | 14,654,302 | 历史安全副本 |
| Assets/Scenes/XingHaiHuiLang/Backups/SC_DarkDungeon_BeforeStructureLight.unity | 13,040,013 | 历史安全副本 |
| Assets/Scenes/XingHaiHuiLang/Backups/SC_DarkDungeon_Exploration_T61_PreviewSafety.unity | 16,447,302 | 预览安全副本 |
| Assets/Scenes/XingHaiHuiLang/Backups/SC_DarkDungeon_Exploration_T62_PreviewSafety.unity | 16,558,447 | 预览安全副本 |
| Assets/Scenes/XingHaiHuiLang/Backups/SC_Sunset_T61_ResumeSafety.unity | 96,349 | 恢复安全副本 |
| Assets/Scenes/XingHaiHuiLang/Backups/SC_SunsetCart_BeforeBlockLight.unity | 70,393 | 历史安全副本 |
| Assets/Scenes/XingHaiHuiLang/Backups/SC_SunsetCart_BeforeHD2D.unity | 42,235 | 历史安全副本 |
| Assets/Scenes/XingHaiHuiLang/Backups/SC_SunsetCart_BeforeRenderRefine.unity | 42,234 | 历史安全副本 |
| Assets/Scenes/XingHaiHuiLang/Backups/SC_SunsetCart_Validation_T61_PreviewSafety.unity | 96,349 | 预览安全副本 |
| Assets/Scenes/XingHaiHuiLang/Backups/SC_SunsetCart_Validation_T62_PreviewSafety.unity | 103,817 | 预览安全副本 |
| Assets/Scenes/XingHaiHuiLang/Backups/SC_VoxelRenderLab_Standard_T61_PreviewSafety.unity | 598,867 | 预览安全副本 |
| Assets/Scenes/XingHaiHuiLang/Backups/SC_VoxelRenderLab_Standard_T62_PreviewSafety.unity | 607,243 | 预览安全副本 |
| Assets/OSSASTRA/Crafting/PrivateCheckpoints/BeforeCrafting.unity | 1,243,363 | 合成实施前私有检查点 |
| Assets/OSSASTRA/WorldProps/PrivateValidation/SC_LegacyCommerceSmoke.unity | 745,703 | 旧业务冒烟私有验证 |

共 17 场和 17 个 meta，64,601,356 字节（61.609 MiB）。其中 Backups 15 场及 meta 为 62,611,932 字节（59.711 MiB）；本次迁往外部归档主要减少项目内验证杂物，不承诺归还这部分磁盘空间。

## 已完成的静态检索

- 首次按精确场景名/路径检索 `Assets/OSSASTRA`、`Assets/XingHaiHuiLang`、`Assets/Scenes/XingHaiHuiLang`、`ProjectSettings`、`docs` 和 `Candidates`。
- 追加对整个 `Assets` 的 `.cs/.json/.asset/.prefab/.unity` 检索，明确排除 `Assets/Script/**`、`Assets/Scripts/**`；17 名称和三个候选目录无命中。
- 再检索 `PreviewSafety`、`ResumeSafety`、`BeforeCrafting`、`LegacyCommerceSmoke`、`Backups/`、`BeforeAnimeV2`、`BeforeDecay`、`BeforePaint`、`BeforeStructureLight`、`BeforeRenderLab`、`BeforeBlockLight`、`BeforeHD2D`、`BeforeRenderRefine`、`PrivateCheckpoints`、`PrivateValidation` 等动态拼接片段，允许的 C# 源码无命中。
- 历史文档命中：`xinghai-sunset-hd2d.md:11`、`xinghai-anime-dungeon-v2.md:21`、`xinghai-sunset-cart-validation.md:5`、`xinghai-anime-dungeon-decay.md:9`、`xinghai-sunset-blocklight.md:9`、`xinghai-dungeon-blocklight.md:11`、`xinghai-dungeon-render-lab.md:52`、`xinghai-cartoon-painted.md:11`。这些描述旧任务当时的备份，不是实际场景入口；原文作为历史保留。

## 必须保留的范围与风险

- `CompositionWindow.OpenScene` 动态拼接四个 `SC_FinalComposition_{Amber,Frost,Dungeon,Sunset}_T7C3.unity` 路径。这四场是本轮渲染来源与现有工具入口，不在删除清单。
- 研究地图、GroundWorkshop、Crafting、Commerce、Visibility 的现存场景有 builder、Map 登记或配对迁移入口。不能仅按“名字旧”批量删。保留当前 RoomLab、原有两 dirty 场、Freycia 独立预览及现存有效主/过渡场。
- 当前磁盘 `EditorBuildSettings.asset` 列出启用的 CommerceIntegration、CommerceTransit 与 `Assets/Scenes/SampleScene2.unity`。这与前序聊天摘要中的“RoomLab buildIndex=2”不一致。必须以当前 Native 精确读回核对；本审计没有更改它，也不将旧摘要作为删场依据。
- 本轮基线已有 31 个 M01/M02/ROADMAP 等路径被删除，属于用户/其他任务已有改动，不夹带到本次发布或恢复。
- 检索排除了用户明确禁止读取的旧脚本目录；无完整全项目文本保证。Native 反向依赖与构建/地址索引门禁仍是删除前的必要条件。
- `.unity`、`.prefab`、`.asset`、`.meta` 只经 Unity API 修改；本报告不是对这些资产的文本补丁。

## 交接

Root 完成 Native 门禁与归档/删除后，在正式交付文档记录真实删除数量、归档路径、GUID/反向引用结果和主场复验。本文当前状态仅为静态清理审计完成。
