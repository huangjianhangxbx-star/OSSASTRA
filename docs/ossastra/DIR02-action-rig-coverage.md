# DIR02 动作与Rig覆盖

本表是现有玩法的表现适配，不是原游戏技能复制。全部138源动画保留，但仅77个本项目逻辑ID有显式Mapping；具名近似可用，不能理解为全部源动作玩法已经移植。

| 当前入口/语义 | 配置小蓝片段 | 状态/政策 |
|---|---|---|
| Idle/CombatIdle/Crouch/Turn | _stand / _stand2 / _standpeace / _standturn | 采用；有效方向，零新方向保留 |
| Walk/Jog/Back/Fast及转接 | _move；结束转_stand | 采用；沿原移动仲裁与朝向 |
| 普通四段 | a1 / a2 / a3 / a4 | 采用；原玩法接受/释放/恢复时间，按原机制匹配速率；四段空挥实际通过 |
| 闪避/Dash/Avoid | _dash | 采用；消费实际接受的MotionVelocity |
| Hurt/Stagger/Death | _damaged / _stun / _die | 采用；全身独占保留合法结构层，死亡仍遵守原隐藏/返场规则 |
| 防御Enter/Hold/Exit | skill_架盾1 / 2 / 4 | 采用；持续防御消费真实GuardDirection |
| HeavyAttack | a0.5大戳刺 | 采用为候选表现；不修改伤害或费用 |
| RocketAttack/副手投掷语义 | skill_丢冰矛2 | PRESENTATION_PARTIAL：冰矛形象仅语义近似现有发射/投掷效果 |
| RushPrepare/Charge/Release | skill_盾冲前1 / skill_盾冲！ | 采用为盾冲候选；按已接受能力/运动方向 |
| RocketLeap | skill_飞跃2 | 候选；保持现有落点/碰撞/费用 |
| Reload/PrecisionReload | _pickup | PRESENTATION_PARTIAL：没有声称该片段是原生枪械换弹 |
| Fire/MagazineShot | a4.5丢矛 | PRESENTATION_PARTIAL：作为枪械射击的近似身体表现 |
| Jump/Land | _jump / _land | 候选；不新增跳跃玩法 |
| 通用Ability、SkillNotice/Ready、Skill1–16 | skill_大戳 | PRESENTATION_PARTIAL：显式近似，不统一退Idle，不代表全部技能设计完成 |
| 五个结构逻辑DirectionFace1–5 | 方向_左 / 左下 / 左上 / 下 / 上 | 0时长、mix0、loop、不随动作时长缩放；默认结构轨9 |

上表是Map的逻辑ID→clip；实际Router可能选择四段逻辑ID作为某技能身体表现。自然键鼠故事中A的Q/R实际分别观察到a3/a4，不能仅凭按键名断言必走HeavyAttack等条目。所有默认可达逻辑引用均指向本模型存在的片段；旧rozeul装备覆盖仍保留，模型范围Override随后把身体片段映射到小蓝。

## 方向表

世界XZ，角度atan2(x,z)，候选相机无Yaw。先匹配规则；67.5/112.5/157.5/202.5/247.5/292.5/337.5/22.5度为边界，闭区间重合时前项胜出。无新方向保留本体最后结果；无历史时initialFace=frontLeft。

| 面ID | 源根/方向片段 | 常规右手 | 常规左手 | a4接管右/左手 |
|---|---|---|---|---|
| left | 左 / 方向_左 | 右手 | 左手 | 右手6 / 左手6 |
| frontLeft | 左下 / 方向_左下 | 右手2 | 左手2 | 右手8 / 左手8 |
| backLeft | 左上 / 方向_左上 | 右手3 | 左手3 | 右手11 / 左手11 |
| front | 下 / 方向_下 | 右手4 | 左手4 | 右手7 / 左手7 |
| back | 上 / 方向_上 | 右手5 | 左手5 | 右手12 / 左手12 |

`equipment.right`、`item.use`用右手列，`equipment.left`用左手列。左右镜像只改变表现镜像，不交换功能手。

a4实际时长0.8333333秒；原Transform约束于0.13333334秒接管、0.6秒返回，Rig以该时段/clipDuration保存归一化覆盖（约0.16至0.72），在面通用规则前匹配。不是按Gameplay时间另建锁。

12片段×5面×10时间（含接管边界）×2镜像=1200实际Spine4.3姿态，左右选中骨均在边界内。该姿态集合含_stand/_move/a1/a2/a3/a4/_dash/_damaged/_stun/架盾2/盾冲/大戳；没有冒称所有138片段都穷举。

## 独立装备边界

复用项目已存在、具名的左右Spine技术探针及其语义Map/grip，非正式武器美术。八向换面保持同Skeleton实例，grip误差检查<0.001世界单位。源武器64个具名slot在独立模式额外mask，参考模式恢复，不删除附件或骨。

排序沿既有整件Renderer相对身体偏移；它不能表达每根手臂/躯干间的局部遮挡，复杂穿插仍PARTIAL。发射/命中范围继续玩法连续向量，未因视觉挂点改变范围或伤害。
