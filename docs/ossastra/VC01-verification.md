# VC01 验证与交付边界

状态：技术候选供集中试玩/架构审查；不代签用户手感、听感或审美。基准47cfc64，原型c21d4fc；已发布G1/配置增量见4f28b13、0d791b8。本文件随收口提交发布。

## 可核验结果
|范围|证据|结果/说明|
|---|---|---|
|可配置连段、恢复呈现、接受账本、技能冷却纯契约|Tests/Combo、Presentation、Coordination、Skill；独立obj|5+4+4+4通过；另有原共享瓶11、装备/盾11纯契约通过|
|最终四段与持续移动闪避|Evidence/configured-actions-release.json|9项通过；600HP本场实际扣72，不以旧72HP目标死亡替代释放验证|
|技能键盘/动画/付款/真实命中/切控后派生|skills-input-release.json|10项通过；共享定义、各自CD/体力与主手来源隔离|
|正常uGUI输入故事|hud-story-final.json|16项通过；虚拟Mouse经InputSystemUIInputModule真实命中按钮；装备窗口即时读数、合法换装、忙时拒绝、点击不穿透、盾/弹/技能/Y/切控投影|
|完整自然故事|natural-story-final.json|52个输入阶段通过，非52个独立测试；故事内没有传送、直接扣血、改余额或重置敌人。仅输入设备/焦点是夹具。四段→两人→装备→盾/弹→Q/E→Y→真实敌杀/切控→合法返场，实例/耐久/共享余额保留|
|能力禁用边界|skill-disable-parallel-green.json|5项通过；同类新配置不改Root可接受，独立CD，禁用只撤自己的未释放主动作，保留已接受副手，不退款|
|模态与独立共享服务|modal-consumable-green.json|4项通过；窗口捕获也拒绝Y/F共享服务，不消耗；释放捕获恢复正常服务|
|控制权/设备许可区分|modal-offhand-clock-green.json|3项通过；输入捕获不撤实控身份、不取消已接受副手、不冻结其实控CD|
|抛物实际素材|projectile-sprite.json|2项通过；接受时冻结本机炸弹sprite与尺寸，离手显示真实sprite，落地回收；旧场无定义保留兼容图形|
|技能配置前置校验|skill-configuration-green.json|5项通过；稳定ID、有限冷却、派生范围及延迟在接受事务前校验|
|坏动画映射|skill-invalid-green.json|缺映射在接受/付款前拒绝，无异常、无新动作、无费用|
|装备挂点/翻转/死亡|equipment-view.json|10项通过；真正Lhand与sprite，角色实例互不污染，死亡当帧隐藏|
|三档六区几何|hud-geometry-1920/1440/1280.json|18个外框通过，1.5px容差；最终代码另检查了三档图片，原型与Unity图片仅本机保存|
|旧场连段/资源/共享瓶定向回归|legacy-chain-final / legacy-resources-final / legacy-healing-final.json|15+18+9通过；旧场无Skills/UI组件，不调用最后新增的主通道按身份取消方法；该方法另有上面的5项定向验证|
|保护与定义无污染|protected-final / configuration-immutable-final.json|119项旧场/Prefab/设置/meta哈希一致；自然故事期间44个自有配置/metadata无文件写回|
|近期错误日志|console-final.json|对应检查时最近300秒cache无error；不是清空全部历史日志后宣称全工程无错误|

## 失败证据与实际修复
- recovery-red：可移动恢复阶段仍保留攻击主轨。用表现占用政策修复；恢复移动不跳过AttackReady，不取消原动作。
- skill-invalid-red：缺映射曾在接受、付款后抛错。现在配置/映射先校验，再进入同一协调事务；green为对应复验。
- skill-disable-red：禁用能力曾遗留未释放动作；第一次修复又暴露全局取消会误撤独立副手（skill-disable-parallel-red）。最终按执行ID只取消主通道，5项green。
- 盾输入曾额外请求炸弹而产生无关拒绝；现按已装备副手种类路由，16项UI复验。中间夹具曾复用非全新世界；保留失败，不算通过。最终UI脚本按游戏帧单步调度并保留间距，避免追赶多步压缩短鼠标边沿。
- 源码曾将UI输入许可当作实控归属，可能冻结/撤回副手；最初探针未建立合法装备前提，不计因果证据。以限定空闲/合法接受的探针复验；契约现区分IsPlayerControlOwner与IsPlayerInputAllowed，3项green。
- 模态曾只阻挡角色帧而漏过场景共享瓶服务；modal-consumable-red保留，统一查询Current真实输入资格后4项green，旧共享瓶9项另复验。
- HUD圆形裁剪曾缺CanvasRenderer，已补明确依赖。中间点击夹具有虚拟/硬件设备混读；测试独占虚拟UI绑定后通过，未用Button.onClick直接调用充当鼠标输入证据。缩放由Canvas单一所有者管理，避免CanvasScaler重复写入。
- 旧场首次回归沿用了1280视口，旧调试面板遮住夹具瞄点，普攻被正确的UI拦截拒绝。保留 red-small-viewport 证据；恢复原视口后，15/18/9重新全部通过。没有修改旧场来迎合测试。
- skills-input-stale-pointer-fixture：传送夹具后未更新鼠标，派生落点与预期目标不符；只修夹具并保留原失败。自然故事没有这类传送前提。
- stale-chain-read-not-validation：早期失败切场后读到旧SessionState，明确不计成功。后续最终探针均使用独立key并核对dispatch成功。
- E1不带atlas时的失败仅是缺区域，不能据此叫版本阻塞。带真实图集读取4.1.23、13段姿态和本场实际显示通过，见enemy-compatibility-atlas。

## 审查矩阵落点
- V01/02/10/13/14：规则、110动作目录、12候选实播、SO列表、真实4段及连续向量；不复制8套主角模型，不在玩法按片段名分支。
- V03/04/05：held-axes及最终自然闪避/恢复；原用户Shift失败在冻结基准未复现，不虚构已找到原始根因。虚拟InputSystem通过不等于用户物理键盘已验收。
- V06/07/08/11/12：统一协调与原Runtime/网关；拒绝无付款、坏配置先拒、有效帧单释放；独立副手边界及旧场回归。Root编号调度与装备LateUpdate可直接阅读。
- V09/15/16/17/18/19：两实例、配置文件不污染、身份换装与新配置扩展；主手技能不依赖左盾。原型技能形态为SAMPLE适配，非完整职业复制。
- V20/22/23/24/25：共享服务9项旧场回归、16项正常UI、52阶段自然故事、Z/Tab/F/G键位对账及真实事实反馈。单死与全灭返场沿原服务；自然故事完成后的较晚idle快照有继续战斗/死亡循环，不能当故事结束瞬间快照。
- V21/26/27/28：三档原位素材，E1独立EnemyRoot，119项保护检查，定向旧入口与源码阅读地图。审美、听感、架构接受留给用户。

本轮未做：干净克隆运行/Player构建、完整背包/商人/旅行/成长、全职业库或全部动画的使用、跨平台字体适配。当前Windows动态字体与本机商业媒体依赖见DEPENDENCIES及local-media-manifest。GitHub只发布自有代码/配置/meta/新生成盾图与文字证据；其余媒体本地保留，不同步Gitee。用户原有两项旧禁编asmdef删除不纳入提交。