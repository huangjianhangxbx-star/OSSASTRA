> 本文是第一轮修整记录；当前已继续完成[移动/朝向/DataBus基础修整](character-foundation.md)，以下CharacterMotor和旧接口为历史状态。

# 角色控制第一轮修整 — 2026-10-19

按用户四项明确要求完成本机候选修整；没有新提交/推送，架构仍待用户核查。先读根[OSSASTRA_RULES.md](../../OSSASTRA_RULES.md)。

## 命名和脚本目录

六个纯领域源文件及Ossastra.CharacterCore.asmdef移动到Assets/OSSASTRA/Scripts/Characters/Core，代码内容不变。

| 原运行脚本 | 当前脚本（Assets/OSSASTRA/下） |
|---|---|
| M01CharacterRoot | Scripts/Characters/Runtime/CharacterRoot.cs |
| M01PlayerInput | Scripts/Characters/Runtime/CharacterInputHandler.cs |
| M01Motor | Scripts/Characters/Runtime/CharacterMotor.cs |
| M01SpineView | Scripts/Characters/Runtime/CharacterSpineView.cs |
| M01MeleeHit | Scripts/Characters/Runtime/CharacterMeleeHit.cs |
| M01HealthTarget | Scripts/Development/Combat/CombatTestTarget.cs |

生产适配器命名空间Ossastra.Characters；验证靶Ossastra.Development，与旧global::CharacterRoot/InputHandler区别明确。全部移动经AssetDatabase.MoveAsset，7处移动的脚本/目录meta SHA保持。生产C#没有M01标记、Keyboard.current、Mouse.current或Legacy Input读取。

## 输入配置与参考Prefab

旧实现使用新Input System的底层设备直接读取，本轮改成与旧InputHandler同样可序列化/可检查的InputAction组件；这两种都是新输入系统API，但后者使绑定从代码中分离。

[Unity官方Actions说明](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/Actions.html)支持在MonoBehaviour中序列化InputAction。本机实际InputSystem1.14.4-t1已编译验证。

| 新组件字段 | 真实VELGITA来源 / 当前绑定 |
|---|---|
| movementAction | 克隆movementAction：WASD复合、手柄左摇杆复合，共10 binding记录 |
| mouseAction | 克隆mouseAction：Mouse.position，屏幕坐标 |
| gamepadAimAction | 克隆gamePad_MouseAction：右摇杆，世界XZ方向，不能当屏幕像素用 |
| attackAction | 克隆HoundWeaponAttack_Action：鼠标左键/手柄右扳机 |
| cancelAction / pauseAction | Inspector配置X/P，暂停另有Gamepad.start |
| debugValid/Blocked/Reset/InspectAction | Inspector配置F1–F4 |

动作每个组件独立拥有，OnEnable/OnDisable/OnDestroy分别启用/停用/释放；Read只返回CharacterInputFrame，不写HP/Spine/动作接受状态。Root通过显式组件引用消费输入。当前仍只有一次普攻，不伪称左右手完整攻击/连段迁完；参考Prefab的右键Hunter动作、装填/跳跃等未实现功能未增加假配置。后续通过同一动作配置流程接续。

## 新角色预制体与当前场景

Assets/OSSASTRA/Prefabs/Characters/PlayerCharacter.prefab：

```text
PlayerCharacter
  [CharacterRoot, CharacterInputHandler, CharacterMotor, CharacterMeleeHit, CharacterController]
  VisualFlipRoot
    VisualRoot
      MainBody [CharacterSpineView, SkeletonAnimation]
  EquipmentRoot
  CombatAnchors
  CameraTarget
```

参考VELGITA的Root+独立输入/动作组件和视觉/装备/锚点分组；没有把旧系统全部拖入新工程。Spine原数据/变换保持；体素XZ与CharacterController保持，不复制旧Rigidbody2D/RectTransform坐标体系。

现有SC_M01_CharacterTest测试场已实际连接此Prefab实例，输入/motor/view/melee及场景camera/debugTarget全非空。Prefab自身不保存场景camera/target引用；实例保存对应override，相机未注入时Root可解析当前Camera.main。需要给每个角色配置唯一actorId；本轮未实现队伍主控切换/AI，不应同时启用多个角色的玩家输入来冒充已支持队友AI。

验证靶CombatTestTarget仍是开发辅助，CharacterMeleeHit当前通过该靶取得Health；正式角色受击/队伍战斗仍未迁完。此项没有用通用命名掩盖完成度。

## 验证与保护

- 核心29/29；6个核心C# SHA与修整前一致。
- 输入10/10：自定义J移动、N攻击、双实例独立、真实指针、松键、左摇杆、右摇杆方向、禁用输出清理、禁用一方不影响另一方、重新启用。
- 输入验收是隔离临时预览场+虚拟键鼠/手柄+真实InputAction事件，不是整场Play或物理键鼠手感认证。编辑模式默认不处理玩家Action，早期夹具因此失败；最终夹具使用临时克隆InputSettings，开启本机包支持的编辑模式玩家输入/手动更新，finally恢复原InputSettings对象、删除临时设备/对象/预览场。没有改磁盘输入设置。
- 当前场127碰撞体、missing scripts=0，新Prefab的input/motor/melee/view引用全有，场景6引用全有。
- Unity编译错误0。MCP prepare_editor持久journal在本轮曾报Sharing violation；保留失败回执，不声称该操作ready。后续实际编辑器readiness=true、编译错误查询及实际脚本执行通过。没有触碰Library文件修复工具。
- 一次探针误用当前版本不存在的OpenPreviewScene，仅探针CS0117，已改成受保护附加场读取并恢复用户Prefab Stage；不是产品脚本编译失败。一次带刷新操作Request cancelled后先读回，未盲目重放；最终连接与磁盘保存重新明确验证。
- 用户测试场1_卧室与旧VELGITA.prefab磁盘SHA保持；当前仍卧室dirty、旧VELGITA Prefab Stage，未保存或运行其现场。附加测试场检查会临时离开只读旧Prefab Stage，最终已重新打开原Stage。

当前规则文件/README/同步记录已指向本报告；旧M01逐行解释被标为历史，受影响源码需复审。未启用自动发布，也未同步Gitee。