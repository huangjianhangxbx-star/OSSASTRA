# Shared character controller refinement — implementation plan

用户已明确批准本轮四项修整，2026-10-19。本会话直接执行，不创建代理或自动提交。

## 结构与约束
- Assets/OSSASTRA/Scripts/Characters/Core：原6个纯核心C#与原asmdef，保留GUID。
- Assets/OSSASTRA/Scripts/Characters/Runtime：CharacterRoot、CharacterMotor、CharacterSpineView、CharacterMeleeHit、CharacterInputHandler；命名空间Ossastra.Characters，避免与已解禁的旧global::CharacterRoot混用。
- Assets/OSSASTRA/Scripts/Development/Combat：CombatTestTarget，仅验证靶。
- Assets/OSSASTRA/Prefabs/Characters：从自有验证角色生成可复用预制体，Root/Input/Motor/Melee同根，VisualFlipRoot/VisualRoot、EquipmentRoot、CombatAnchors分组参考VELGITA；不挂旧数据总线/战斗系统。
- 不更改物理平面：XZ/CharacterController沿用已批准体素基准，不复制旧Rigidbody2D。视觉Spine和美术依赖保持原引用/缩放。

## 执行与验证
1. 保存自有代码/场景/meta恢复副本、旧参考Prefab与用户当前场景磁盘SHA；读取真实组件及InputAction绑定。
2. 在外部工作目录保存真实Unity输入验收探针：动态查找新的输入MonoBehaviour，创建隔离组件、配置动作、虚拟键鼠/手柄，用真实InputSystem事件证明可重绑定/移动/攻击/禁启/实例隔离。先运行，应因现有输入类不可配置而RED。
3. AssetDatabase.MoveAsset保持meta/GUID，移动核心目录及逐个重命名运行脚本。普通源码修改命名空间和类型；核心逻辑不变，csproj Compile链接改新路径。
4. InputHandler用序列化InputAction，Read只读动作；每实例Enable/Disable/Dispose自己动作，无Legacy Input回退，无Keyboard/Mouse硬编码读取；摇杆方向与鼠标位置分开。Root用引用的输入组件，并维持原资格/取消/命中闭环。
5. 用Editor API仅加载自有场景为附加场，添加输入组件并克隆VELGITA的movementAction/mouseAction/gamePad_MouseAction/HoundWeaponAttack_Action绑定；取消/暂停/调试动作写入序列化配置。生成自有共享Prefab并接到自有场景，不触碰用户当前卧室或VELGITA编辑现场。
6. 编译ready、输入探针GREEN、核心29/29；核对GUID/Spine引用/未丢脚本与旧参考SHA。用户当前未保存场不进入Play；引擎输入探针在隔离临时场景运行，不用旧场景冒烟。不可把输入探针当整场游戏手感认证。
7. 建立根OSSASTRA_RULES.md，逐条记录用户明确规则/生效范围/日期，发生新决定或冲突时同步更新；README/交接链接它。更新当前审查账本，旧逐行文档标历史需复审，保留未批准状态。
