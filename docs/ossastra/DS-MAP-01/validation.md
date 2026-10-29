# DS-MAP-01 验证记录

基线：main@6ee7f47，Tuanjie 2022.3.62t16 / 1.10.4、URP 14.2.0-t1、Spine 4.3。采用用户补充的滚轮/十字键旋转；包内Q/E旋转及替代技能键安排已被覆盖。范围是B级模块验证，非整款游戏/Player发布验收。

## 分开判定

| 维度 | 结论 |
|---|---|
| 研究可信度 | 本机base版本源码/选定资源可追溯；40个源文件SHA复查一致；原生矩阵、完整autotile/noise仍UNKNOWN |
| 场景素材完整性 | 本机三地表、八类/九模板、40物件及现有Blue完整角色已落地；未使用原作人物 |
| 现有角色旋转适配 | 本轮八角移动/攻击/闪避、原四次点击连段与可选模块回归通过 |
| Unity技术验证 | 下表覆盖项通过；真实OS失焦通知和全编辑器隐藏管线材质增长单列，非全部系统零风险 |
| 原作动态效果对照 | OBSERVATION_BLOCKED：两次启动未取得可操作游戏窗口，不声称原作八角视觉复现通过 |
| 正式工程兼容与隔离 | 原Commerce启动/输入/普攻冒烟通过；42个非授权保护项及40个已用源文件一致 |
| 用户视觉/架构验收 | PENDING_USER，不代签 |

## V01—V19

| 项 | 结果与实际范围 |
|---|---|
| V01 | PASS/SOURCE_VERIFIED：app219740/build12527201/release578406；base数据层；定向转换119/119，原作实机观察受阻另列 |
| V02 | PASS：真实滚轮八次回到原角；MapOrbit 10/10含两向跨界、反向、同时相反请求和有界重复；十字键输入24项合同涵盖，实体手柄未人工操作 |
| V03 | PASS：91项Play检查含40物件根姿态固定、八次静止旋转世界显示方向不变；镜头/显示子层变化 |
| V04 | PASS：八档每档W/S/A/D原输入移动；补充八档斜向请求速度一致。162项数学检查涵盖幅值；真实输入与数值检查不混记 |
| V05 | PASS（选定Blue）：原Prefab变体、原Root/仲裁/动画/装备链；八视角截图仅一面；待机/移动/攻击沿原SO映射；未另写Motor |
| V06 | PASS（代表样本）：8张实际画面逐张QA，树草苗石箱火坑完整，无整sheet/正常视角细线；视窗裁切不算每个边缘实例全貌证明 |
| V07 | PASS（样本限定）：真实按键绕树录像及石/箱/3D角绕行截图；树与立体块实际重叠时深度遮挡有效。非所有交叉半透明材料的通用证明 |
| V08 | PASS：真实forest/grass/dirt与原图alpha边缘；草地有周期纹理感。四角mask选择为UNITY_ADAPTATION，不冒充原生autotile |
| V09 | PASS/边界：Input24、Play禁启/捕获与回调；F7/F8经PlayerLoop事件实际进入。工具simulate_key_press会手动InputSystem.Update，曾吞短边沿；改用排队后由PlayerLoop处理，不改产品。Windows焦点切换后Application.isFocused仍true，真实OS回调未证明；false/true回调释放另经实际组件验证 |
| V10 | PASS：F7侧视/F8俯视截图含真实纸片、脚点、占地、网格、碰撞AABB/来源；观察机不成为人物瞄准相机，返回恢复，TimeScale=1 |
| V11 | PASS：三轮从磁盘重开并Play；首次生成明确拒覆盖。59项本机依赖预检10/10（假缺失不删素材、真实零缺失）；公开壳不能靠Import/Build自动修复既有GUID |
| V12 | PASS（本模块）/外部增长：三轮均Root1/Actor1/Props40/Camera2/启用Listener1/临时0；本模块DepthPlane材质33均持久资产、运行时0。全Editor材质1583→1693→1849，主要为Hidden URP/HDRP/GDRP；不声称全编辑器材质无增长，不在此轮改全局管线 |
| V13 | PASS：可选模块缺省回退及所有权检查；原主场/芙蕾茜娅场/两基础Prefab/Graphics/Quality等指纹一致，原dirty保护，构建列表未加本场 |
| V14 | PASS：原SC_CommerceIntegration实控Blue无ViewSpace/MapRoot，W沿原世界Z、攻击受理且一次释放；Q/E原键盘和LB组合绑定保留 |
| V15 | PASS：源码、窄配置、引用壳与纯文本证据允许发布；Klei媒体/原Lua、私有帧表、Mesh和物件Prefab仅本机，.gitignore与公共资产内容审计通过 |
| V16 | PASS（选定动作）：8角各单击攻击并在前摇旋转，48项身份/同相主轨/费用/单次释放/真实目标HP检查；4次点击4段，8角闪避机动/费用快照。未全面遍历所有装备技能 |
| V17 | PASS/范围：Root历史13/13，实际新移动/空闲手柄世界历史、键鼠world fallback、AI世界向量不二次旋转；现场无伙伴AI，全队实战非本轮对象 |
| V18 | PASS：Root初始化/Tick/派生表现/释放，单一输入、运动、方向与主轨权威；程序集检查Characters.ViewSpace不引用MapResearch/Spine/Input |
| V19 | PASS：Q/E继续原技能；滚轮/十字键为新镜头入口，原键盘方向键保留。虚拟输入租约清理、原场冒烟及保存绑定检查，实体手柄手感待用户 |

## 关键收据

公开纯文本在Candidates/DS-MAP-01/Evidence与Tests；截图/录像及完整转换清单仅本机研究根。

- ViewSpace：RED缺API→162/162；生命周期真实RED35/2→37/37（Edit显式Suspend，不能冒充Play）。
- 世界手柄历史：RED9/4→13/13；MapOrbit10/10；不等时物件帧7/7；输入租约24/24。
- LabMovementPlay：91/91；LabCombatPlay：48通过/1失败，失败是把原“点击接段”误设为“长按自动四段”的夹具假设。
- LabLifecyclePlay：31/31，用四次真实点击验证原四段；含单次闪避、结构输入捕获及Root/Adapter/MapRoot三轮禁启。
- FinalMotionPlay：25/25，补齐8角斜速/闪避/转镜头不改机动快照、费用token稳定及失焦回调释放。
- 首次场景生成曾误清组件原生m_GameObject导致组件失效和编辑器崩溃。已保护原生归属字段、只映射业务引用，实际生成成功、42保护项一致；不把该事故隐去。
- 首次Play因-300 OnEnable早于Adapter启用而拒绝绑定；改为Start首绑/后续OnEnable重绑后实际通过，物件不再显示整张图集。

## 性能与空间

同一1600×900 Editor GameView，RTX4080SUPER/i7-14700KF，quality5/VSync1/targetFPS-1；无录像时各180帧。idle delta中位2.209ms/P95 2.957ms；行走+旋转1.993ms/P95 2.679ms；采样末159 draw calls/159 batches/794 triangles。它们是Editor帧delta与当时统计，不是独立Player CPU/GPU或最终帧率承诺。全Editor managed约1.328GB、Unity allocated约686MB，不归因给本模块；Hidden管线材质增长见V12。未为数字修改画质、管线或帧率限制。

研究目录约123MB（含工具构建/证据），Unity本机媒体约9.17MB、私有生成资产约0.14MB；精选源+转换约23.1MB，低于1GiB软预算。没有完整解包游戏、人物或音频。公开仓库不含这些受限媒体。

## 未完成的人工/外部事项

原作实机动态对照、实体手柄手感、全部复杂半透明/柔边/受光美术验收、用户整体视觉/架构审查未代签；本轮未运行Player构建或全贸易/音声/历史场回归。菜单的跨机GUID修复不在本轮，公开壳明确依赖同机已生成本机资产。
