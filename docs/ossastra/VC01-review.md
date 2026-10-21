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
