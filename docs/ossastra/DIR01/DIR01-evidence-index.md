# DIR01 证据与验收实账

2026-10-24。商业源数据、图像/视频、原作IL和含许可信息的官方CLI日志**只留本机**。此仓库发布自有研究脚本、计数/检查结果与说明，不发布原作数据副本。原作研究为 STATIC_CONFIRMED / ORIGINAL_RUNTIME_NOT_RUN。

本机研究根：`E:/WORLDCREATOR/XingHaiHuiLang/work/OSSASTRA/DIR01-20261024`。下表文件均相对该根；完整JSON不是公开仓库中索引JSON的替代品。

| 证据 | 内容及读取方式 |
|---|---|
| assets-manifest.json | 三个源文件与历史原型样本SHA；PNG 4096×4096、单atlas页、PMA=true |
| recovered/Blue_DIR01.spine | 官方已安装4.1.24导入副本，CLI再次读取成功；GUI编辑/保存未做 |
| evidence/unpack-validation-local.json | 192region存在及尺寸、87旋转、源SHA复核 |
| native43/native-read-analysis.json | 原xl.spine与副本SHA相同；4.3.26官方readback/导出，unit骨名/动画名无差集 |
| native43/readback-local.log、export-local.log | 两Skeleton、4.3.26和约束数；副本图片路径警告；可能含个人许可信息，不公开 |
| DIR01-rig-direction-map.json | 776骨/494slot的完整分支、附件/权重、约束、所有方向原始属性、动作writer与deform/drawOrder；仅本机完整数据 |
| evidence/original-*-local.json | 受限原作程序集的定向方法IL、token、偏移；未运行游戏，不上传原作代码 |
| evidence/runtime-readback-local.json | 29检查、真实骨/手世界位置、轨身份/事件、显隐候选、mix中间姿态、有限CPU采样 |
| evidence/boundaries-local.json | 300真实姿态、起始裁切激活、约束开关前后、15长兵器采样6483附件、边界抖动 |
| evidence/01-all-branches.png | A/Setup，全骨架、中文/拼音根、原点和遮罩；真实截图 |
| evidence/02-five-directions.png | a1/25%，五面同一时刻，方向轨9，镜像关，矩形剔除等效，角色近景；真实五格 |
| evidence/03-preview-controls.png | E模式完整操作页截图 |
| evidence/04-direction-switch.webm | 主动作固定25%，约4秒转向；前半mix0、后半mix0.4 |
| protected-before.json | 原四项修改文件SHA；另31项原删除保持 |

## 原作定向静态证据

游戏Build24935935；读取元数据/IL，不加载执行游戏程序集。优先复用现有CR02/CR04/CRAR01，补充仅相关方法。

| 方法/证据文件 | 关键位置与结论 |
|---|---|
| original-direction-sectors / tables / range | SpineUtil.方向转spine名 token0x06000255；八向表、闭区间优先匹配、zero左下、可选addWidth |
| 原有CR04 il-unit-forward-spine | Unit.SetForward取XZ→GetAnimName；Character.banRotate/目标方向分开 |
| original-unit-play | Unit.Play token0x0600284C；方向SetAnimation轨9在IL026A附近；循环提前返回IL02E5..031A之后；inheritTime在IL033C..0394 |
| original-unit-late | Unit.LogicLateUpdate token0x0600282B；RefreshRotation后，IL0021调用手动更新遮罩 |
| original-clipping | SpineUtil.手动更新遮罩 token0x0600025E；IL012F..0154按slot.bone.WorldX/Y和Rect.Contains赋HideBy高级控制 |
| original-slot-alpha | Slot.get_A：IL0001读隐藏字段，隐藏IL0008返回0，否则原alpha；Attachment getter正常 |
| original-mesh-generator | 三个定向MeshGenerator方法调用Slot.get_A，闭合显隐渲染链 |
| original-bone-position | GetBonePos/GetBonePosition精确骨名查询＋Transform；不证明自动按面选骨 |

程序集SHA256：

- Assembly-CSharp：`51977661202D164037ABAA502A701E64C9426E8B6351EA6200263033C3188D44`
- Assembly-CSharp-firstpass：`1B97B1644E9DD3551825EFA81F35E8066C60C6D36FF037C4C269D69ABE9C2FF7`
- spine-csharp：`1F91E4D2DCAC9EBF7733DE46C04463AFBDE36D412ED4A8D8EB4A3B3502BEA5EF`
- spine-unity：`D6F087753253D28B64DBEDE067CB1199536B2252677BA559B1589AC8AF3E8B64`

原型定向源码blob匹配计划基准（本机未切分支）：reference-spine41 `864e25915d04fcd0aca3cafa63725ba3d82cc56f`；blue-direction-mask `b969d626e803e4b68caf2c91c2c3aa7e78d1de33`。镜头固定倾斜观察世界地面；其负heading是原型约定，不直接作为Unity相机方向约定。预览提供原作角度/原型角度两种显式输入，不把两者同一数值当同一世界朝向。

## 手骨实测例（a1 / 25%，模型坐标）

| 面 | 镜像关：左手 / 右手 | 镜像开：左手 / 右手 |
|---|---|---|
| 左 | (-15.12,33.51) / (21.55,45.35) | (15.12,33.51) / (-21.55,45.35) |
| 左上 | (1.75,37.66) / (26.41,39.77) | (-1.75,37.66) / (-26.41,39.77) |
| 上 | (-2.44,37.58) / (20.78,38.82) | (2.44,37.58) / (-20.78,38.82) |
| 左下 | (-14.37,30.94) / (9.94,39.87) | (14.37,30.94) / (-9.94,39.87) |
| 下 | (0.62,22.02) / (-21.56,28.87) | (-0.62,22.02) / (21.56,28.87) |

这只是当前中文面同名手骨候选，不代表全部辅助分支接管时点的正式武器锚点。完整候选在本机JSON，尚未给多面角色挂入独立装备。

## D01–D18

PASS指本轮有界研究问题已回答；PARTIAL明确保留未穷举或未实机部分，不把计数凑成全通过。

| ID | 状态 | 证据与限制 |
|---|---|---|
| D01 | PASS | 三源SHA/历史一致；native4.3与export4.1分开 |
| D02 | PASS（CLI＋独立播放） | 官方拆图/导入/readback；192尺寸通过；GUI打开保存未操作 |
| D03 | PARTIAL | 五中文/八拼音依赖有据；各辅助支所有138动作的具体接管未穷举，不标废弃 |
| D04 | PASS | 五条完整属性矩阵只含translate；五面镜像原点求解 |
| D05 | PASS | 四裁切的起止/权重/draworder；scale三动作启用；原作额外显隐链 |
| D06 | PASS | 六代表动作writer/deform/drawOrder，300姿态；一clip写多子树 |
| D07 | PASS（静态） | XZ→atan2→表→镜像→轨9；原作未运行 |
| D08 | PASS | 0/1、动作来源优先级、中文清理与原作所有slot的差异 |
| D09 | PASS（隔离） | 同主轨实例、时间/事件不重启，zero/boundary/jitter；不涉及玩法费用 |
| D10 | PASS（隔离） | 五面×镜像、源裁切/原型/矩形的真实显示和候选数 |
| D11 | PARTIAL | 原作banRotate/动作方向/阶段采样静态；独立攻击/移动/闪避/受击可运行；未逐原作技能实测 |
| D12 | PASS（有界） | 15跨支约束；关约束漂移/恢复；不删除任何分支，不宣称全动作可删性 |
| D13 | PASS | 当前Controller/Composer/FactsKey源码；正式MergePlans三分支探针复现清轨，未改生产 |
| D14 | PARTIAL | 五面左右手世界坐标/镜像已测；正式独立装备多面遮挡和功能锚点未接入 |
| D15 | PASS（隔离） | 匹配runtime、两实例Data共享但姿态独立、暂停/步进/重建；未升级生产 |
| D16 | PASS | 唯一5187研究台六模式、动作/面/裁切/进度/挂点、可重启launcher |
| D17 | PASS | 原作静态/原型适配/Unity缺口/建议/未知分账；DIR02仅草案 |
| D18 | PASS（范围保护） | 原源SHA与35原dirty保护；B内存dirty保留；仅自有资料定向GitHub |

## 工具与复现边界

官方Spine launcher 4.2.18，默认Editor4.3.26；旧数据副本使用已缓存4.1.24（4.1.23未安装），播放器4.1.56，Unity/Tuanjie2022.3.62t16/Spine4.3。未安装第三方Spine Skill/MCP，也未下载安装浏览器或其他依赖。第三方候选只有文档评估，所有API仍NOT_TESTED，不冒称READ_OK。

恢复方式遵循[官方导入/拆图说明](https://esotericsoftware.com/blog/Importing-skeleton-data)，版本隔离遵循[官方版本说明](https://esotericsoftware.com/spine-versioning)，叠轨依据[官方动画应用说明](https://esotericsoftware.com/spine-applying-animations)。它们说明通用工具规则，不代替本模型和原作证据。

浏览器29项与边界6项全部通过；300姿态是采样数，不是300个独立功能测试。一次探索性边缘断言原先预设“应观察到越界”失败，实测0；现如实记录0与有限范围，不人为缩遮罩制造失败。纯研究脚本在Candidates，不参与Unity生产编译。
