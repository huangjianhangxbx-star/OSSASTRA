# FREYCIA 独立模型试玩

日期：2026-10-29。本轮把用户当前芙蕾茜娅模型接入独立试玩入口，用来观察五面切换与移动动画。原有小蓝、rozeul 两个角色及其场景保持原样。

## 打开与操作

在 Unity 打开 `Assets/OSSASTRA/Scenes/SC_FreyciaPreview.unity`，进入 Play，再点一下 Game 视图取得输入焦点。

- WASD、方向键或手柄左摇杆：移动，并按移动方向切换五面与左右镜像。
- 停止移动：回到模型的静止姿态，保留最后显示方向。
- 当前不开放攻击、闪避、装备或技能；本次导出没有相应的完整动画映射，不用 Move 冒充这些动作。

该场景只有一个新的 `freycia-preview` 角色根和一个 FREYCIA 身体，另有测试地面、固定相机及只读操作说明。没有带入原两个角色、双人协调器、旧装备或背包系统。

预制体：`Assets/OSSASTRA/Prefabs/Characters/FreyciaPreview/FreyciaPreview.prefab`。它可单独放入其他自有预览场；角色根通过现有单角色输入入口运行，场景需要提供 MainCamera 和可站立地面。

## 素材与缩放

用户源目录：`E:/正式项目模型/星骸回廊/芙蕾茜娅/游戏文件`。

本机导入目录：`Assets/OSSASTRA/LocalMedia/FreyciaPreview/`，骨骼数据资产为 `FREYCIA_SkeletonData.asset`。源 `FREYCIA.skel.bytes`、`FREYCIA.atlas.txt` 和 `FREYCIA.png` 只读使用，本轮不修改源导出。

- Spine 版本：4.3.26。
- `SkeletonDataAsset.scale = 0.001`，即一个源单位对应 0.001 Unity 单位。
- 身体和角色根 Transform Scale 均为 `(1, 1, 1)`，没有按旧角色高度或整套阵列 bounds 自动适配。
- 身体与固定相机使用相同的 60° X 旋转，保持当前项目的 XZ 行走平面。
- 图集没有声明 PMA，当前 Atlas 按 `pma=false` 解析并使用 Straight Alpha 输入；项目色彩空间与渲染管线没有改变。

模型、图集、贴图及其本机派生资源保留在本机导入区，不随本轮代码与作者配置自动公开。干净克隆需要补齐获准使用的模型及项目既有插件后，再在 Editor 核对引用。

## 动画与方向配置

专属配置位于 `Assets/OSSASTRA/Configs/Characters/FreyciaPreview/`：

| 配置 | 用途 |
|---|---|
| `FreyciaPreview_Animation.asset` | 原式语义枚举到本模型片段的映射 |
| `FreyciaPreview_Direction.asset` | 五面根、八向扇区、镜像及显隐方式 |
| `FreyciaPreview_Character.asset` | 独立角色定义及以上方向配置引用 |
| `FreyciaPreview_InitializationAction.asset` | 满足原角色根初始化约束的合法时间线副本，不表示已接入攻击 |
| `FreyciaPreview_Floor.mat` | 本预览地面的独立 URP Unlit 材质 |

本次只接入下列七条真实片段：

| 语义 | 原生片段 | 时长与用途 |
|---|---|---|
| Idle | `animation` | 0 秒；空动画，显示静止的 setup pose，并非新增一套待机动作 |
| Jog | `Move` | 约 0.7667 秒；五面共用的循环移动动作 |
| DirectionFace1 | `居中/侧面` | 0 秒；侧面根居中 |
| DirectionFace2 | `居中/侧正面` | 0 秒；侧正面根居中 |
| DirectionFace3 | `居中/测背面` | 0 秒；侧背面根居中，保留源文件实际拼写“测” |
| DirectionFace4 | `居中/正面` | 0 秒；正面根居中 |
| DirectionFace5 | `居中/背面` | 0 秒；背面根居中 |

五个根骨骼的实际名称为 `侧面`、`侧正面`、`侧背面`、`正面`、`背面`。`居中/测背面` 对应的根仍然是 `侧背面`；不要在代码中凭名字相似替换。导出的 `Run2` 只覆盖部分面，因此未作为全方向移动映射。

动作使用轨 0，方向居中片段使用轨 9；全部 mix 为 0。方向片段不随动作时长缩放，也不排队或清除主轨。当前模型原生侧面朝右，镜像约定与小蓝配置不同：

| 移动方向 | 面 | 镜像 |
|---|---|---|
| W / 世界 +Z | 背面 | 否 |
| W+D | 侧背面 | 否 |
| D / 世界 +X | 侧面 | 否 |
| S+D | 侧正面 | 否 |
| S / 世界 -Z | 正面 | 否 |
| S+A | 侧正面 | 是 |
| A / 世界 -X | 侧面 | 是 |
| W+A | 侧背面 | 是 |

方向由 `atan2(x, z)` 计算，边界取现有顺序优先规则；玩法方向仍是原连续向量，不被五面显示扇区量化。

## 复用的运行链与显隐边界

实际移动仍走现有 `CharacterInputHandler → CharacterRoot / StateArbitrator → CharacterMovement`。显示方向沿 `CharacterOrientation / CharacterDataBus → CharacterDirectionPresentation`，再和动作意图一起进入原 `CharacterAnimationController → Config / Composer / AnimationHandleSystem → Spine`。没有新建第二个角色更新循环、输入轮询或动画计时器。

这个模型没有 `ClippingAttachment`，因此专属方向配置显式选用 `FaceBranches`：绑定骨架时按五个互不嵌套的面根缓存骨骼和槽位归属；当前面之外的 Region/Mesh 槽位只在本实例构网格期间暂时设为透明。共同根上的共享槽位保留。下次应用动画前、构网格后或组件停用时恢复原透明度，不改共享骨架数据、附件、骨骼或约束。

旧的 `ClippingBoundary` 模式仍是序列化默认值 0，小蓝继续使用原裁切边界规则。换面只改变表现，不换角色身份、不移动碰撞体，也不改变移动或攻击的玩法向量。

`CharacterModelPreview` 只在原 Root 初始化后用独立 owner 登记攻击限制；组件停用或移除时释放自己的令牌。初始化动作副本没有攻击动画映射，当前模型也没有假装完成攻击接入。后续提供完整动画时，应补专属 SO 映射并重新验收，再解除这项预览限制。

## 首次生成工具

纯文本作者脚本：`Candidates/FreyciaPreview/BuildFreyciaPreview.cs.txt`。该文件位于 Assets 外，不参与游戏编译；由唯一 Unity writer 在 Edit、程序集 ready、当前场景 clean 时，经项目 MCP 的 `IFunplayCommand` 入口显式执行。

生成器只用于首次创建，发现任一目标场景、Prefab 或配置已存在便拒绝覆盖。已有作者调整应通过 Inspector 或针对明确资产的 Editor API 修改，不删除成品来反复运行生成器。

生成器要求模型先导入到上述本机路径并确认 0.001 缩放。Spine 4.3 组件先空建、再绑定 Renderer 数据；Single 新建场景后会从 AssetDatabase 重新加载已保存配置，避免继续使用被卸载的 SO 包装。只保存自有新资产和新场景，不修改 Build Settings 或旧场景。

## 验证记录

- 真实 Spine 定向夹具 66/66 通过，包含五面分支显隐及旧裁切模式回归。
- 实际 Play 中八个键盘移动方向均得到上述面与镜像结果。
- 作者脚本与预览组件对当前 Unity 程序集静态编译通过，0 错误、0 警告。

- 实际 Play 停止移动后主轨回到 `animation`，保留最后显示方向；骨架重新初始化后显隐与结构轨正常恢复，鼠标左键没有受理动作或产生释放。
- 保存后重开场景，再进入 Play，实测 S 输入仍沿原角色链移动：主轨 `Move`、结构轨 `居中/正面`、缩放 0.001、单 root / 单身体。
- 最终停在已保存的独立试玩场 Edit/ready，场景 clean。Console 在最后验证窗口没有运行错误。
- 36 项非登记表原 dirty 文件的存在状态和 SHA256 与接手基线一致；原主场 `SC_CommerceIntegration` 无本轮改动，三个源素材 SHA256 不变。登记表只维护本任务记录，未提交其他任务的改动。

本机 Game View 截图在 `artifacts/freycia-preview/`。这些检查不代替实体手柄或用户美术观感验收。原始回执位于本机 `E:/WORLDCREATOR/XingHaiHuiLang/work/OSSASTRA/FREYCIA-PREVIEW-20261029/`。
