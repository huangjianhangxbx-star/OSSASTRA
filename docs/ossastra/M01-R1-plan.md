# M01-R1 v0.5 实施取舍与阶段A对照
基准6fdd36a；用户当前指令授权实施包中范围，文档草稿状态不覆盖人类当前授权。包SHA256 FEF4ED01FBA794A29017C58ABF74BFC4F02D7613CF663FA7D1578C4412F711FC。
旧Root以ICharacterSubsystem.Initialize(Root)、ICharacterTickable.ManagedUpdate与固定显式次序组织；候选基准Root则具体类型Initialize/Read/Orient/Request/Advance/Move/Present。旧设计有通用生命周期，但隐式root多功能访问和庞杂Tick不必全搬。
C1：具体序列化槽保持Inspector可查；Awake注册为ICharacterInputSource/Movement/Orientation/Presentation/HitResolver，Bus只读依赖接口；仅Movement/Presentation实现ICharacterSubsystem.Initialize(DataBus)；HitResolver收完整释放参数，无Bus依赖和假初始化义务。顺序显式，输入/朝向无假Initialize。C2统一ManagedUpdate难表达前置输入、Released命中与后置动画的不同阶段，暂不选。
现行问题：Orientation.Direction同时写FacingX；Root每帧先Orient再Release，Melee即时读Bus.Direction，没有接受方向锁定；Spine按同一FacingX显示。修复为VisualFacingX/AttackDirection两个状态，输入Frame提供move/pointer/stick；合法鼠标XZ优先，再stick、move、上次方向。界外/UI指针无效，零向量保留方向。可动左右输入决定显示；Accepted/Released时FaceAttackDirection；释放帧方向作为Vector3值传给HitResolver，不新建动作时钟。
Root移出Development引用/OnGUI/测试命令；Development.CharacterDebugControls订阅显式只读Input并调用Root通用取消/暂停/禁攻/传送入口，owner为自身。HitResolver通过ICharacterHitTarget识别原开发靶，不建新Health。输入支持开发UI登记阻挡区域，阻挡区/UGUI下鼠标不攻击、方向回退；游戏手柄不因闲置鼠标压UI失效。
执行：真实旧版R17 RED→退出Play→实现方向/接口/开发隔离→Prefab自有场更新→重跑29+10+12+14（接口更名适配说明）→R17物理左右靶与显示/释放时采样、替换实现/初始化顺序/生命周期测试→全源码/行号/说明/图/权威/接线/SHA/Diff/待审账本→停用户审查，无自动发布/M02。

