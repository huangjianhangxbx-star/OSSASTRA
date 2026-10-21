# VC01 实施与审查入口

基准：47cfc64。执行用户附件VC01 v1.0（本地work/OSSASTRA/VC01-20261021/spec.md）；不实施总表VC02/03。

- [x] G0：用户规则登记R24—R28；rozeul目录已实际导出，12候选实播、用户确认c21基准、三档实屏及旧链对账已完成，见VC01-baseline。
- [ ] G1：真实持续移动输入复现；抽出统一动作政策/事务，Root显式组织；失败请求无破坏性副作用。
- [ ] G2：AnimationProfile/ActionDefinition SO、事件派生冻结时间线、配置列表连段、闪避与恢复呈现；保留旧场兼容入口。
- [ ] G3：装备定义与独立实例/能力/挂点/正常换装命令；E1原型近战敌资源独立记录。
- [ ] G4：两类代表技能及Y定义；新配置不改Root，资源/派生身份保持。
- [ ] G5：UGUI迁移原型六区及资产；真实数据/命令/音画反馈，开发面板默认关闭。
- [ ] G6：自然输入故事与受影响合同；三档对照、源码审查、GitHub定向发布并停步。

## 模块与测试落点
- 现有Root/Input/Movement/SpineView为适配点；通用模块保留Scripts内，不放模型目录。
- 角色协调器消费只读生命/权限/资源及动作端口，独立主副手不共享一个busy。
- 动作定义持有不可变配置/阶段标记，接受时冻结；Health/Posture/Gateway/Current仍为既有权威。
- 测试放Candidates/VC01/Tests，独立obj/bin；虚拟InputSystem测试与物理键鼠人工验收分开。
- 资产/场景/SO仅经Unity Editor API创建保存，旧场默认只读。原始商业媒体不推送。

## 当前发现
- 当前Origin为86824bd，与附件c21d4fc基准有分叉；HUD/命令基线已询问，依赖部分先保留。
- Spine实际资源Assets/随便尝试/rozeul_SkeletonData.asset，目录已从运行库读取；尚未把名称等同实播用途。
- ChatGPT链接登录墙，不可读取新增正文。
- Skill：writing-plans、brainstorming、diagnosing-bugs、TDD、项目记忆和Funplay MCP已读；UGUI用于后续界面迁移。未安装的superpowers子技能不冒称使用，采用本任务G门顺序执行。

## G1 当前已验证增量
CharacterActionCoordinator成为普攻/闪避/盾起手/投弹的最终接受与付款入口，Root组织初始化，窄接口集中CharacterContracts。每请求有Actor/生命/控制代际、来源、请求身份；原执行端保留动作身份/状态/CD。维护Trace SourceLabel以便区分玩家Stage与伙伴来源。
接受账本纯4；实际错误Actor/生命/控制/来源4项拒绝无副作用；持续移动后Shift18、盾10、主副手投弹13、资源与伙伴收费/返场18通过。恢复移动主轨修复纯4/真实3通过。拒绝不调用提交端口、不付款、不清现有动作。没有把虚拟输入通过写成真人键盘已验收。
本阶段仍有配置化连段/技能加入后的再验证；G2—G6尚未完成。临时能力的重复早期依赖检查保留为防御，但接受/付款最终均走同一端口；无新Update/Move/Health权威。
