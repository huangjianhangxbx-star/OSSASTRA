# DIR02 验收记录

2026-10-25；本机Tuanjie2022.3.62t16 / Spine4.3，源4.3.26。受测工作树从a7efd119开始；产品提交SHA由Git历史确认。保护范围为接手36项dirty，不将其混入发布。

## 实际测试集合

- DirectionContracts：10；ComposerContracts：5；VisibilityContracts：5；ModelOverrideContracts：1；RigRouteContracts：5；RigEmptySlotContract：显式空slot+历史校验2条件。先有失败证据，再实现/修正；最终相关合同复跑为0失败。
- EquipmentPoseContracts：1200姿态，0失败。HandPoseProbe最初发现a4十个场外默认挂点，随后以源时间线和具名辅助手修正，不能把这次发现抹掉。
- DirectionInputStory：42步骤，0失败；虚拟InputSystem手柄/键盘，AI隔离夹具。八向静止转向、独立装备同实例、四段各一次释放、移动态闪避、举盾转向、Z切rozeul普攻再回小蓝。
- DirectionLifecycle：10条件，0失败；明确是边界注入。实际Skeleton重建/禁启/OS暂停/失焦/Root禁启/伤害管线死亡/正常返场服务。
- NaturalCombatStory：16步骤，0失败；AI开启，无传送/直接伤害/资源回填，正常键鼠接敌、格挡、普攻、Q/R。敌人HP600→526，伙伴也参与；此HP总差不能全归因小蓝。55秒无声录屏已保存并抽帧。
- MixedInputRegression：36步骤，0失败；原AM03流程移到新对照场，虚拟键鼠/手柄、悬停/UI不抢向、控制权切换及失焦释放。首轮旧夹具UI输入未隔离且错误输出Vector2发生JSON循环，最终自有副本显式绑定UI设备并将诊断向量转字符串后通过；未因此改生产输入代码。
- NativeDirectionContract：5步骤，0失败。原生E经正常LB+Y受理，前摇后退时身体仍优先接受的能力方向；改变右摇杆不改本次锁向，结束后回有效瞄准。先捕获到反向MotionVelocity错误优先，再修正并复验。
- 保存/重开候选：2Root、1A身体、Variant、B无方向模块、缺脚本0。最后一次生产源码刷新后六组合同复验通过；域重载/Play重入实际发生。

## 24项合同对应证据

| 项 | 结果 | 证据与边界 |
|---|---|---|
| T01 | PASS | 4.3.26 unit真实导入、192图片/atlas、Linear未改、源SHA相同 |
| T02 | PASS | Variant/重开只读检查，原Prefab/主场保护 |
| T03 | PASS | 10方向合同与实际8向输入；世界XZ、顺序边界声明 |
| T04 | PASS | 静止8向含同X符号内换面 |
| T05 | PASS | 同TrackEntry/ActionId进度保持、每段1次Release，未新增付款路径 |
| T06 | PASS | 合法0时长结构合同，旧根只恢复所拥有的X/Y |
| T07 | PASS | 原式Composer独占合同及四段/闪避/死亡/格挡实际播放；受击/破势姿态采样 |
| T08 | PASS | 非法source/普通高轨不豁免；rozeul无轨9 |
| T09 | PASS（所选政策） | 主动作按真实Release、持续GuardDirection与接受的MotionVelocity/能力上下文；不量化玩法向量 |
| T10 | PASS | 36混合回归；多面模块不采设备，AI沿原有效Bus；非实体手感验收 |
| T11 | PASS | 骨/边界共同Skeleton空间，镜像采样，真实首帧/重建；不以世界位移更改局部mask |
| T12 | PASS（所选动作） | 776骨/全部约束保留，a1/a2/a4真实姿态与片段近景；非全部138动作 |
| T13 | PASS | 0.37透明度恢复、附件引用不变、隐出隐入；不改共享skin/attachment |
| T14 | PASS | 两个实际Skeleton共用Data，显隐alpha互不污染；实例方向不写SO |
| T15 | PASS / PRESENTATION_PARTIAL | 77显式逻辑映射、默认输入故事；枪械/通用能力近似见覆盖表 |
| T16 | PASS（1200姿态范围） | 30条Rig、a4具名辅助接管、镜像不交换功能手 |
| T17 | PASS | 八向装备Skeleton实例ID保持、无玩法装备重建；资源真变才Bind |
| T18 | PASS | 两种模式的实际渲染门控与具名内置slot可逆mask |
| T19 | PASS / PARTIAL | 实际双手grip<0.001；复杂内部穿插未做，不宣称完美遮挡 |
| T20 | PASS | 10生命周期边界；OS暂停是系统挂起，不恢复游戏暂停功能 |
| T21 | PASS（职责/不写玩法） | 方向/显隐/装备只消费事实，无额外Release/付款路径；诊断只影响mask |
| T22 | PASS | 所选单面bounds比例1.30202186，仅视觉节点；物理根/相机不随选面移动 |
| T23 | PASS | 保存重开/源码域重载、缺脚本0、rozeul切控普攻及混合输入 |
| T24 | PASS（本轮有界） | 正常实战、同进度八向媒体、作者入口/指南；正式美术/实体手感等待用户 |

## 有限性能观察

ProfilerRecorder对整帧GC成功取到180帧，均值约241616字节/帧（包含Editor、整个游戏、夹具，不能归因方向模块）。本机未能取得自定义Visibility标记，`Valid=false`，0样本不是0成本；开启Profiler再试仍无此标记，恢复原Profiler开关，未包装为通过的耗时测量。

另在Unity主线程对真实当前姿态额外执行1000次mask.Apply+Restore，测得约12.8641微秒/次，采样API报告分配0字节；这是局部同步微基准，不能推导完整Spine、装备、帧率或正式机型成本。方向选择/静态slot及Rig索引缓存避免新加逐帧名字查找；源全部骨/约束仍求解，不声称只计算一面。

## 本机媒体与保护

`Candidates/DIR02/LocalEvidence/a4-42-*.png`：同a4 42%八向；离屏姿态证据，非输入故事。`body-first.png`、`combat-after.png`及Library/FunplayMcp/Recordings中的55秒视频仅本机。离屏预览首轮未设PreviewScene剔除掩码导致空图，已修正相机scene/culling后重生成并目视抽查，不使用空图验收。

五份受保护修改逐项SHA相同；31项原删除未改；源Spine SHA为CEA88B22BD2C1539AEF081D0FA0B76720B3A0AC6F6C22ED779430049B22F3196。受限媒体与旧商业IL/许可证记录不发布。
