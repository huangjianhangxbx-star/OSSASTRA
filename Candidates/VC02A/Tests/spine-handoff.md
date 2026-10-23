# VC02A A6 装备 Spine 接线交接

本分工仅提供一套可执行的独立装备技术链，不代表全部 A6 或正式装备美术验收完成。

## 实现

- `EquipmentDefinitionSO.spinePresentation` 引用 `EquipmentSpinePresentationSO`；作者配置角色骨骼/可选插槽、装备内部握持骨骼、缩放/旋转/排序及所有身体动画语义。
- `CharacterEquipmentSpineView` 没有自己的 Update/LateUpdate，由角色 Root 初始化与 LateUpdate 推进。两个手槽分别持有独立 Skeleton/AnimationState/播放身份，组件不含输入、HP、伤害或费用。
- 正式调用顺序为角色语义提交、身体 Spine 求骨骼、Root LateUpdate 装备手动更新与握持对齐、Spine Renderer 构建网格。装备动画 UpdateTiming 明确为 ManualUpdate，不会额外自主推进。
- Accepted 冻结装备动画目录；主身体执行 ID 与身体阶段重播 ID 分开记录。装备片段名称/长度可以不同，结束/事件不触发玩法结算。
- 骨骼矩阵定位与镜像、插槽颜色/附件显隐、内部握持位置/角度、排序分别实现。候选显式关闭插槽附件显隐，仅跟随存在的插槽颜色；不将这个配置选择冒称已测过全部组合。
- 缺映射或挂点显式报告，不静默回退 idle。死亡/禁用隐藏、卸装销毁本实例子对象；身体 Skeleton 重建后重新绑定。
- 旧 `CharacterEquipmentView.cs` 未修改。构筑脚本仅将自有候选盾的 `worldSprite` 设空，避免旧 Sprite 路径与新 Spine 同时显示；其他旧场配置保持原样。

## 技术探针和执行

1. 主代理完成 Root 初始化/TickPresentation/Hide 接线并确认编译。
2. Edit 执行 `spine-configure.cs.txt`。仅自有 `SC_VC02A_CombatMigration`；dirty 先另存恢复副本。Editor API 创建 `Configs/Abilities/EquipmentSpineProbe` 下的纹理、材质、atlas、独立 SkeletonData 与表现 SO。
3. 技术盾为原创几何网格与中心标记，3 骨骼/2 附件/8 动画，使用安装的 Spine 4.3.26 运行库。它不复制角色、不把 Sprite 或空 GameObject 冒充装备 Spine。非正式美术。
4. Play 执行 `spine-world.cs.txt`；读取 SessionState `VC02_EQUIPMENT_SPINE`，完成后扫描控制台，保存失败及实际绿证据。
5. 保存重开/域重载与候选截图由唯一 Unity writer 集成执行。

## 测试与当前事实

- `dotnet run --project Candidates/VC02A/Tests/spine-animation.Tests.csproj`：新增类型前编译失败；实现后 9 项纯契约通过。覆盖映射快照、显式缺失、相同身份不重复提交、新阶段同名重播、双实例、取消换语义、重复语义/非法速率拒绝。
- Unity 第一轮暴露旧全局 AnimationIntent/AnimationPlaybackIdentity 名称遮蔽；已使用与 CharacterSpineView 相同的显式命名空间别名修复。
- 实际 Unity 编译、构筑及 Play 测试由主代理执行，本分工不提前记 PASS。
- 无身体占用的独立副手释放仍沿显式身体语义表现；本次没有建立新的独立动画权威或从装备事件反写玩法。

## 文件与中文注释审计

新增生产文件 `EquipmentAnimationCatalog.cs`、`EquipmentSpinePresentationSO.cs`、`CharacterEquipmentSpineView.cs` 均有中文职责/边界总览；冻结、阶段身份、骨骼镜像与内部握持逆变换有中文解释。`EquipmentDefinitionSO.cs` 仅新增表现配置字段，现有内容定义职责注释仍准确。测试源有中文用途说明。全部生产脚本位于 Scripts，不和探针素材混放；未修改第三方、旧角色表现或 .meta/序列化文本。

本分工释放 EquipmentDefinitionSO.cs，主代理可以继续扩展 A4 actionProfiles。其他新文件继续由主代理接管，后续失败按实际证据修复。
