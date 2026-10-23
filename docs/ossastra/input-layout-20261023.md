# 键鼠与手柄输入布局

本轮依据用户2026-10-23截图实现。手柄只覆盖探索/战斗的常用操作；不要求覆盖种田等全部鼠标功能。世界没有玩家暂停功能，菜单也不暂停。

## 配置入口

统一配置资产：`Assets/OSSASTRA/VC02B/PlayerInputLayout.asset`，类型 `PlayerInputLayoutSO`。

它包含移动、鼠标、右摇杆瞄准、左右手、闪避、三技能、交互、血瓶、切人、武器模式、背包、菜单及前四格快捷引用。修改时停止Play，在该SO的Actions中编辑Bindings。下一次进入游戏时各消费者克隆独立InputAction，两个角色及场景服务读取同一份布局，但启用/禁用状态不共享。

角色 `CharacterInputHandler.inputLayout` 非空时，**SO优先于组件下方的本地Action**；旧场未指定SO仍使用本地配置。`PlayerInputLayoutInstaller` 在场景早期把切人、瓶、交互、背包、菜单、热栏连接到相同布局，它是场景接线组件，不是角色状态权威。

`PlayerCharacter.prefab`已引用该SO；B综合场两个人物也引用它。清理了两个场景实例中220项旧输入字段override，保留其他场景属性。之前Prefab绑定与场景绑定不一致，是场景的旧输入覆盖项仍在生效；现在不再手工维护两套按键副本。

`rightHandAction / leftHandAction` 是序列化字段，Inspector显示Right Hand Action / Left Hand Action，Action名称是 `RightHandAction / LeftHandAction`。FormerlySerializedAs迁移旧字段，旧源码访问属性仅兼容现有消费者，不持有第二份Action。

## 当前绑定

|操作|键鼠|手柄|
|---|---|---|
|移动|沿用户原移动配置|左摇杆|
|瞄准|鼠标位置|右摇杆方向|
|交互|F|A = Button South|
|闪避|Space|B = Button East|
|右手/主手|鼠标左键|LT = Left Trigger|
|左手/副手|鼠标右键|RT = Right Trigger|
|技能1|Q|LB + X（West）|
|技能2/固定E槽|E|LB + Y（North）|
|技能3|R|LB + B（East）|
|共享血瓶|Y|LB + A（South）|
|切换角色|Z|LB + RB|
|主手动作模式|未增设键盘默认键，可自行配置|LT + RT|
|背包|Tab|Start|
|菜单|未增设键盘默认键，可自行配置|Select|
|前四格道具引用|1、2、3、4|RB + X、Y、B、A|

武器模式切换调用原 `CharacterMainAction.Configure`，仍需当前装备有动作组，并满足脱战空闲等原有条件；无动作组的装备不会凭空生成第二种模式。切换不改变当前技能费用和动作结算。

RB四格目前沿已有热栏引用/选择语义，**未实现用户明确延期的连续道具使用、方向标、等待主武器确认投掷、空格退出瞄准**。尚不支持的鼠标细操作不冒充已有手柄适配。

## 如何绑定组合键

使用新输入系统内置 `Button With One Modifier` Composite：

1. 给目标Action添加此Composite。
2. Modifier绑定 `<Gamepad>/leftShoulder`（LB）或rightShoulder（RB）。
3. Button绑定实际面键，例如X使用 `<Gamepad>/buttonWest`。
4. 同一Action另加普通键盘Binding，例如技能1另加 `<Keyboard>/q`。

LB+RB的Modifier为leftShoulder、Button为rightShoulder。LT+RT的Modifier为leftTrigger、Button为rightTrigger。不要把两个普通Binding当作“同时按”，两个普通Binding代表任一个即可。

运行时会阻止带肩键修饰的面键透传到基础闪避/交互，例如LB+B不闪避、LB+A不打开箱子。识别使用该Action实际输入设备，避免用另一支手柄的肩键影响当前输入。

双扳机组合识别宽限由SO `triggerChordGraceSeconds` 配置，当前0.08秒。单侧扳机等待该窗口后送出手部操作；窗口内按下双侧只请求模式切换。双侧组合松开一侧时不会突然发出剩余手部操作，须松开两侧重新按。已经超过窗口受理的攻击不会因为之后补按另一侧而回滚。

菜单是沿当前背包皮肤构造的最小正常uGUI窗口，包含继续游戏/背包，支持再次按Select关闭。它捕获玩家输入，世界、敌人与模拟时间继续运行；不引入暂停设置。菜单中用当前EventSystem的导航/确认配置操作按钮。

## 验证与边界

- `Candidates/InputLayout/Evidence/schema-red.json`：修改前左右手命名/模式/共享SO入口不存在，实际RED。
- `contracts-final.json`：Play内虚拟设备26项通过，包含面键位置、组合绑定、输入互斥、双扳机松开一侧不误施放、Space、独立克隆。此边界夹具将宽限置0以即时检查映射，不能据此声称测得0.08秒时序性能。
- `world-final.json`：实际B场8项通过，含共享SO独立实例、Start开关背包、Select开关可见菜单、菜单中模拟钟继续、LB+RB切Current、LT+RT改变真实模式。使用默认宽限。
- `menu.png`：原生2560×1440 GameView缩至1280×720观察菜单；不是第二个原生分辨率测试。
- 首个编辑态探针17项失败，原因是Editor输入更新与Play动作采样不同；一次不存在Update重载的探针编译错误也保留。改为Play和明确测试输入设置后通过，没有放宽生产接受门禁。

尚未进行实体手柄硬件长时间试玩、连接/拔插、多手柄并发、完整UI手柄无鼠标导航验收；本轮采用虚拟设备经真实InputSystem与实际场景消费者。每次改变绑定后需重新进入Play，当前不提供游戏内重绑保存界面。
