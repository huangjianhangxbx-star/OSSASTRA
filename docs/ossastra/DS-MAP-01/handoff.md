# DS-MAP-01 交接

## 现在可以打开

Unity菜单：Tools → OSSASTRA → 地图研究 → 打开饥荒地图样地。

场景：Assets/OSSASTRA/Scenes/Research/SC_DontStarveMapLab.unity。独立角色：Assets/OSSASTRA/MapResearch/Prefabs/PlayerCharacter_MapView.prefab，来自原Commerce场Blue五面实例和原基础Prefab，保留控制/仲裁/动画/装备/资源链。原主场、芙蕾茜娅预览、两个基础角色Prefab未覆盖。

滚轮上/下或手柄十字键左右转45°；PageUp/PageDown缩放；Home恢复；F6结构模式，F7侧看，F8俯视。Q/E仍为原技能、方向键仍移动。进入结构会捕获角色输入；持续Held动作按原捕获规则结束，退出先松开再新按。

## 实际交付

- M0：本机身份、项目/素材/场景/输入和代码基线，非授权dirty/资源指纹保护。
- M1：相机、tile4单位、连续物件坐标、Billboard/地面朝向研究，区分SOURCE_VERIFIED/UNITY_ADAPTATION/UNKNOWN；原作实机OBSERVATION_BLOCKED明确保留。
- M2：定向转换三地表及八类/九模板的完整物件图集，119转换检查；无人物/音频/游戏全量提取。
- M3：Root管理可选ViewSpace；玩家唯一意图转换，AI世界向量和原动作/主轨权威保留；Characters不反向依赖地图实验。
- M4：32×32m手工样地、40物件、真实地表边缘、固定世界碰撞与少量自有3D诊断。
- M5：冻结正常显示的独立结构观察相机、真实平面/脚点/占地/碰撞诊断；本机物件Prefab可复用。
- M6：实际八角移动91、攻击48、点击连段/生命周期31、斜速/闪避25及162/37/13/10/7/24模块检查；原场移动/攻击冒烟、三轮重开及保护审计。详细失败/限制见validation.md。
- M7：本目录研究/映射/实现/指南/验收文档，公开测试源码与纯文本证据；独立GPL转换工具保留许可；只向GitHub定向发布，不含受限素材及原dirty。

## 本机依赖与证据

研究根：E:/WORLDCREATOR/XingHaiHuiLang/研究/饥荒地图与视角/DS-MAP-01/。

- 03-converted/asset-manifest.json：本机定向素材/帧/哈希清单。
- tools/：本轮转换与验证脚本；01-research/：原始证据/联系图。
- tests/：实际夹具与完整收据；04-evidence/attack-orbit.mp4、tree-walk.mp4：运行录像。
- 项目Candidates/DS-MAP-01/*.png：八方向、结构、代表遮挡截图，本机保留不公开。
- Assets/OSSASTRA/Art/DontStarveReferenceLocal：40张本机素材贴图。
- Assets/OSSASTRA/MapResearch/LocalGenerated：9物件Prefab及10 Mesh，含受限UV/帧/时长派生信息，严格忽略。

公开Scene/ActorPrefab/材质是带引用的配置壳，不包含原作图片或私有帧表。干净Git克隆缺上述本机资源时不能直接完整运行；菜单会预检缺失依赖，不能把Import或首次Build当成既有GUID的自动修复工具。本机完整场景已经保存重开验证；首次生成拒绝覆盖。

## 已知边界

- 原作两次启动没有取得可操作窗口，未做原作动态视觉对照；native投影矩阵、完整边缘噪声/选tile算法仍未证实。
- 本地深度Shader是实验适配；不代表正式Spine受光、柔边/多层半透明/交叉纸片的最终方案。地表存在可见周期纹理感。
- 按住自动打四段不是原设计，第一次夹具的此项失败保留；四次点击连段已通过。
- 本模块无残留运行时材质；全Editor Hidden管线材质跨Play增长另记，不宣称引擎总体无增长，也未擅改管线。
- Windows激活窗口测试未观察到Application.isFocused变false；实际组件失焦回调释放通过，真实OS通知仍待人工复验。
- 人工视觉/架构、实体手柄、全部装备技能、正式地图系统及Player构建不在已签技术结论中。

## 后续复查入口

先按user-guide.md绕行、滚轮旋转和F7/F8看结构，再看implementation.md的Root时序、世界/观察方向和动画所有权；原作证据见research-findings.md与source-to-unity-mapping.md。V01—V19逐项范围与性能见validation.md。没有自动继续正式地图/生成世界/导航或换模型。

当前已完成工程与限定技术验证；用户视觉/架构验收保留。发布产品提交号在publication-receipt.md登记。最终停在已保存的新样地Edit，不执行休眠/关机。
