> 历史基础修整记录，当前方向语义/窄接口/开发隔离已继续调整；以[M01-R1](M01-R1-review.md)的真实代码和源码清单为准。

# 角色基础职责与DataBus — 2026-10-19

用户批准“现在小范围基础修整”。本轮在前次共享控制/InputAction候选上继续，未增加连段、技能、AI、装备或新的HP状态，也未自动提交/推送。

## 当前实际脚本

全部位于Assets/OSSASTRA/Scripts/Characters/Runtime。

| 脚本 | 唯一负责的事 | 主要依赖 |
|---|---|---|
| CharacterRoot | 验证引用、初始化次序、输入请求/动作推进的调用次序、暂停/失焦/禁启与开发入口 | 显式组件引用、动作运行时与权限所有者 |
| CharacterInputHandler | InputAction采样与唯一CurrentFrame发布，禁用时清空 | 每实例独立的可序列化InputAction |
| CharacterMovement | 位移、CharacterController碰撞移动、实际水平速度/是否移动 | DataBus.Input；Root给出的移动许可和delta |
| CharacterOrientation | XZ方向和左右朝向；OrientTowards不移动Transform | 外部传入的世界目标点 |
| CharacterDataBus | 一个角色内的强类型、只读状态访问入口 | 输入/移动/朝向/动作/权限的原始所有者 |
| CharacterSpineView | 初始化合法clip、播放/去重、暂停与显示翻转 | DataBus读取动作身份、移动和朝向 |
| CharacterMeleeHit | 在真实Release执行空间检测，并交给唯一HP结算 | DataBus.Direction、既有AttackRelease/Health |

原CharacterMotor经AssetDatabase重命名为CharacterMovement，GUID保留。Root的motor字段用FormerlySerializedAs迁移到movement，原speed/groundStick配置保留；新增orientation显式Inspector引用。同步枚举器Actions()保留；没有新增协程。

## DataBus的实际规则

每个Root在Awake创建自己的CharacterDataBus。它不是MonoBehaviour，所以不需要也不会作为额外组件挂在Inspector中；实际组件关联在Root的input/movement/orientation/view/melee等字段检查。

```text
Root.Awake
  验证注册组件与输入配置
  → 创建BasicAttackDefinition / BasicActionRuntime
  → new CharacterDataBus(本角色身份、各组件、动作运行时、控制权)
  → movement.Initialize(DataBus)
  → melee.Initialize(DataBus)
  → view.Initialize(DataBus)
  → ready

Root.Update
  input.Read() → InputHandler.CurrentFrame
  → DataBus.Input（返回值类型快照）
  → orientation.OrientTowards（鼠标地面点或手柄方向）
  → AttackIntent / runtime.Request / runtime.Advance
      Released → melee.Resolve → DataBus.Direction → 碰撞检测 → 唯一Health结算
  → movement.Tick（DataBus.Input.move + Root明确计算的许可）
  → view.Present（DataBus实时读取动作/位移结果/朝向）
```

| DataBus属性 | 真实写入者 | 不做什么 |
|---|---|---|
| Input | InputHandler.CurrentFrame | 不再独立保存第二份输入 |
| IsMoving / Velocity | CharacterMovement | 不由动画反推速度 |
| Direction / FacingX | CharacterOrientation | 不在Movement另存方向 |
| ActionActive / ActionId / MoveReady / AttackReady | BasicActionRuntime | 不复制动作时钟或结束标记 |
| AllowsMovement / AllowsAttack | ControlAuthority | 不用一个canAct替代所有权限 |

AllowsMovement仅代表控制权允许，MoveReady仅代表动作恢复节点；Root另结合焦点/暂停计算最终移动许可。两者有意分开。

DataBus不公开Set接口，也不把BasicActionRuntime/ControlAuthority作为可任意修改的公共引用。Input返回struct拷贝，消费者改拷贝不能改输入所有者。HP当前仍由验证靶引用的Health独占，角色HP模块尚未正式迁入；此轮没有新增虚假的DataBus.hp、角色HP或第二条扣血逻辑。

这保留旧Root/子系统与角色内部数据统一入口的使用方式，同时不照搬旧DataBus里尚无实现消费者的字段。

## 命名与资产

- CharacterInputFrame的move/pointer/aimDirection/attackHeld/cancel/pause/hasPointer/hasDirectionalAim/debugCommand统一camelCase。
- debugPanel为字段，改camelCase；方法、类、只读属性保持PascalCase。
- 已有PlayerCharacter.prefab新增CharacterOrientation，Root显式绑定；测试场继承共享Prefab并保存自身camera/debugTarget引用。
- CharacterController仍是原3D胶囊角色运动组件，本轮没有改变地面平面或物理方案。
- 新/改源码均在专用Scripts目录，不与模型/材质混放。用户规则见根OSSASTRA_RULES.md，当前全部受影响代码仍待用户审查。

## 验证边界

核心合同29项；输入10项；DataBus与朝向12项，均实测通过。新增12项覆盖真实方向归一/不位移、双角色朝向与owner限制隔离、动作接受/恢复/取消状态即时可见、InputAction真实采样及返回快照不可反写、Bus没有公开写入口。

模块探针在临时预览场执行，短暂使用克隆InputSettings以支持编辑模式玩家输入，finally恢复原设置并清理自有设备/对象。不能代替物理键鼠手感验收。
本轮还在实际SC_M01_CharacterTest中完成44步骤/14项回归，最终14/14：输入→atk1→真实HP100→88、按住不重复、释放前后取消、owner禁攻、空挥、领域与Spine暂停/恢复、失焦/禁启Held清理、真实移动run/停步idle、HP归零。测试采用虚拟InputSystem设备和显式焦点生命周期注入，不等同物理键鼠手感认证。

首轮空挥项曾失败（其余13通过）：实际读回发现真实Mouse位置幅值大于虚拟Mouse，Value类型指针Action选择了真实Mouse。定向探针证实，将测试Action临时限定到测试鼠标后读值正确。只修正夹具并恢复原binding overrides，没有为过测改产品逻辑。首轮失败与最终结果均保留。

最终退出Play，独立测试场Edit ready/clean；运行测试设备0，原InputSettings对象/动态更新模式恢复。共享Prefab：movement/orientation/input/view/melee引用全有、缺失脚本0、speed=3、groundStick=2、原移动/普攻绑定10/2。CharacterMovement.meta与原Motor.meta SHA一致；旧卧室场景和VELGITA参考Prefab磁盘SHA保持。本轮开始实际只有自有M01场已加载且clean、无PrefabStage，因此未运行或保存旧用户场景。

原始证据工作目录：E:/WORLDCREATOR/XingHaiHuiLang/work/OSSASTRA/character-foundation-20261019。可复用模块探针：Candidates/M01/Tests/FoundationContract.cs.txt；输入探针同目录InputBehaviorContract.cs.txt。它们是由Unity MCP execute_code执行的命令源码，不冒充Unity Test Runner已注册测试。
