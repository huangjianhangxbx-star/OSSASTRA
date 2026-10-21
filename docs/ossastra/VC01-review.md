# VC01 实施与审查入口

状态：技术候选待集中审查。已完成本轮G0—G6实施/定向验证，用户手感、听感与架构接受未代签。

[内容与调用链](VC01-content-hud.md) · [验证/失败修复/限制](VC01-verification.md)。正式候选场 `Assets/OSSASTRA/Scenes/SC_CharacterVerticalSlice.unity`；停止于此，不自动VC02。

基准：47cfc64。执行用户附件VC01 v1.0（本地work/OSSASTRA/VC01-20261021/spec.md）；不实施总表VC02/03。

- [x] G0：用户规则登记R24—R28；rozeul目录已实际导出，12候选实播、用户确认c21基准、三档实屏及旧链对账已完成，见VC01-baseline。
- [x] G1：持续移动分帧输入验证（原用户故障未在冻结基准重现）；抽出统一动作政策/事务，Root显式组织；失败请求无破坏性副作用。
- [x] G2：AnimationProfile/ActionDefinition SO、事件派生冻结时间线、配置列表连段、闪避与恢复呈现；保留旧场兼容入口。
- [x] G3：装备定义与独立实例/能力/挂点/正常换装命令；E1原型近战敌资源独立记录。
- [x] G4：两类代表技能及Y定义；新配置不改Root，资源/派生身份保持。
- [x] G5：UGUI迁移原型六区及资产；真实数据/命令/音画反馈，开发面板默认关闭。
- [x] G6：自然输入故事与受影响合同；三档对照、源码审查、GitHub定向发布并停步。

## 模块与测试落点
- 现有Root/Input/Movement/SpineView为适配点；通用模块保留Scripts内，不放模型目录。
- 角色协调器消费只读生命/权限/资源及动作端口，独立主副手不共享一个busy。
- 动作定义持有不可变配置/阶段标记，接受时冻结；Health/Posture/Gateway/Current仍为既有权威。
- 测试放Candidates/VC01/Tests，独立obj/bin；虚拟InputSystem测试与物理键鼠人工验收分开。
- 资产/场景/SO仅经Unity Editor API创建保存，旧场默认只读。原始商业媒体不推送。

## 当前发现
- Origin本机86824bd是已冻结c21d4fc的祖先；按用户选择导出c21只读副本，不切Origin分支。
- Spine实际资源Assets/随便尝试/rozeul_SkeletonData.asset，目录已从运行库读取；尚未把名称等同实播用途。
- ChatGPT链接登录墙，不可读取新增正文。
- Skill：writing-plans、brainstorming、diagnosing-bugs、TDD、项目记忆和Funplay MCP已读；UGUI用于后续界面迁移。未安装的superpowers子技能不冒称使用，采用本任务G门顺序执行。

## G1 当前已验证增量
CharacterActionCoordinator成为普攻/闪避/盾起手/投弹的最终接受与付款入口，Root组织初始化，窄接口集中CharacterContracts。每请求有Actor/生命/控制代际、来源、请求身份；原执行端保留动作身份/状态/CD。维护Trace SourceLabel以便区分玩家Stage与伙伴来源。
接受账本纯4；实际错误Actor/生命/控制/来源4项拒绝无副作用；持续移动后Shift18、盾10、主副手投弹13、资源与伙伴收费/返场18通过。恢复移动主轨修复纯4/真实3通过。拒绝不调用提交端口、不付款、不清现有动作。没有把虚拟输入通过写成真人键盘已验收。
以下为已发布G1时的阶段记录；G2—G6最终结果见本页顶部入口。临时能力的重复早期依赖检查保留为防御，但接受/付款最终均走同一端口；无新Update/Move/Health权威。

## G2 与 G3 手持表现增量（2026-10-21）
- 实际配置 rozeul 四段完整普攻；Release=hit_start、MoveReady/AttackReady=hit_to_idle、Finished=片段结束是本轮明确 SAMPLE 映射，不由 Spine 回调再结算伤害。
- CharacterDefinition / ActionDefinition / AnimationProfile 均为 SO；动作接受冻结播放方案和伤害时点；原运行时仍拥有唯一时钟。旧场保留兼容配置。
- 新装备定义接回原 EquipmentState 的实例 ID/耐久/CD；角色根 LateUpdate 读取真实 Lhand 挂点。刀仍是模型内附件，不复制一个刀渲染器。
- 用户批准缺盾时生成匹配素材；生成图保存 Assets/OSSASTRA/Art/Equipment/RozeulShield_v1.png。透明 PNG 1263x1246，Sprite PPU1024。来源为内置 imagegen，本轮新生成；未改旧媒体。提示词约束：单个独立透明盾，匹配 rozeul 的 chibi 深蓝/黄铜/蓝色，清晰描边与手绘阴影，实用尖底鸢盾，占画布80%，小尺寸可读，无手/角色/字/背景，轻微厚度；参考候选场 rozeul 截图仅作风格参考。
- 炸弹图引用既有本机第三方 Bomb.png；不上传第三方纹理，不宣称干净克隆包含商业资源。盾图仅提供左手显示，尚不代表 G3 正常 UI 交互收口。
- 当前代码复验：configured-actions-final.json 9项（四段实际动画/释放/独立付款、持续移动闪避）；equipment-view.json 10项（实际挂点、左右翻转、装备定义换装、实例隔离、非法定义拒绝、死亡当帧隐藏）；纯契约 Combo5 + Presentation4 + Coordination4。
- 之前 fresh legacy-chain 15项通过；stale-chain-read-not-validation.json 是失败切场后读到旧 SessionState，明确不计作验证。
- 以上为0d791b8阶段历史。最终技术候选已接入后续各门，仍由用户集中审查手感/架构。
