# FREYCIA 独立模型试玩接入

- 任务 ID：`freycia-preview`
- 状态：`complete`（本机接入、验证与GitHub定向发布完成）
- 写入模式：已释放；root唯一操作Unity，子代理核对素材、作者工具与文档。
- 日期：2026-10-29
- 独占：新 `Assets/OSSASTRA/LocalMedia/FreyciaPreview/`、`Assets/OSSASTRA/Configs/Characters/FreyciaPreview/`、`Assets/OSSASTRA/Prefabs/Characters/FreyciaPreview/`、`Assets/OSSASTRA/Scenes/SC_FreyciaPreview.unity`及meta；必要的独立作者工具/说明和本任务同步。
- 来源：用户授权将 `E:/正式项目模型/星骸回廊/芙蕾茜娅/游戏文件` 的当前FREYCIA模型装到游戏中试玩；骨骼数据导入scale=0.001，不与现有两个角色混合。
- 必要适配范围：CharacterDirectionProfileSO / CharacterSlotVisibility / CharacterDirectionPresentation 新增默认关闭的FaceBranches显隐模式；旧ClippingBoundary序列化默认0保留。独立开发预览组件只锁定未提供动画的攻击入口，不改共享输入或Root。

## 设计与边界

采用现有五面显示与SO动画映射，单独预制体/独立试玩场仅保留新模型本体与测试地面/相机/输入所需组装。按实际导出骨骼、片段和图集配置，不猜命名，不替换现有两个角色，也不把缺失攻击动画硬映射成其他动作。原源文件只读。R11定向发布自有接线与文档；模型媒体按本机导入区保留，不自动公开原画/骨骼媒体。

## 计划

- [x] 接手当前Editor与Git；SC_CommerceIntegration Edit/ready/clean，HEAD f39e1a5，37项原dirty保护。
- [x] 核对FREYCIA版本、五面根/居中/Move及图集。
- [x] 导入独立资源，SkeletonDataAsset.scale=0.001，创建专用方向/动画配置与单模型试玩入口。
- [x] 实际Play验证移动、朝向、五面/镜像、动画播放、离屏分支隐藏、缩放与重载；保存回读。
- [x] 写清入口/现有动画范围与保护审计、定向GitHub发布并释放租约。

## 证据

原始回执/指纹/导入分析在 `E:/WORLDCREATOR/XingHaiHuiLang/work/OSSASTRA/FREYCIA-PREVIEW-20261029/`。新脚本只有确需适配时才增加；优先配置现有完整链。

## 完成与验证

- 入口：`Assets/OSSASTRA/Scenes/SC_FreyciaPreview.unity`，已保存并重开，Edit/ready/clean；只有一个新Root和一个身体，不混入原两个角色。
- 实际数据：Spine 4.3.26，281骨、102槽、五面、10动画。SDA.scale=0.001，Transform.scale=1。Idle为空`animation`的setup pose，移动为完整`Move`；没有攻击映射。
- 新模式只用于明确配置的FaceBranches；旧序列化默认0仍为ClippingBoundary。真实夹具红测缺API后实现，最终66/66通过，含40组面/镜像/Move阶段与小蓝旧模式回归。
- 实际PlayerLoop输入验证8方向，Move/结构轨/镜像均正确；停止、骨架重建、攻击限制与保存重开后移动通过。没有用软件注入替代实体手柄实测声明。
- 4.3作者API差异通过创建顺序和切场后重新加载本轮配置解决，未修改Spine插件。
- 接手36项非登记表dirty状态/指纹不变；SC_CommerceIntegration无本轮改动，用户三份源素材指纹不变。
- 代码、配置、预制体、场景与说明定向提交；LocalMedia原图/骨骼/图集及派生媒体资产不公开。既有37项dirty不混入提交。

详见 `docs/ossastra/FREYCIA-preview.md`。用户美术观感与实体手柄验收仍由实际试玩确认。

## 发布回执

实现提交 `d99c48a6db7d3b08d889d56c0786c402ace05a4b` 已推送至 GitHub `huangjianhangxbx-star/OSSASTRA` 的 `main`，推送后远端SHA与本地一致。28个定向文件，不含LocalMedia媒体或接手37项旧dirty；本记录收尾只补发布事实。
