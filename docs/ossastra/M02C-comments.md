# M02-C 中文注释核对

| 文件 | 文件头 | 关键同步说明 |
|---|---|---|
| CharacterIntentPolicy.cs | 有，中文职责/边界/协作 | 来源枚举只是Player/Companion/Neutral，不是角色身份或第二个Current。；选择器传入会话存在/许可/本角色是否Current/伙伴源可用性。无会话沿用玩家适配器；有会话但屏障或无Current时Neutral；Current只Player，否则可用伙伴才Companion。无输入读取、状态写入或Unity依赖。 |
| CompanionFollowPolicy.cs | 有，中文职责/边界/协作 | 纯策略依赖System；状态/决策只表达跟随意图与诊断，不是角色动作时钟。readonly决策保存方向，不执行运动。；输入是XZ相对向量、目标绝对坐标、前帧水平实际路程、步长与资格；非法数值/不可行动/非正步长输出Inactive并清策略。 |
| CharacterContracts.cs | 有，中文职责/边界/协作 | 新增只读会话：Current/Characters来自原Coordinator；IsIntentSamplingAllowed是共享屏障，ControlRevision使已采样的旧来源立即失效。Director/Selector是真实消费者。；新增可选玩家实时许可，由原InputHandler实现，Selector消费；解决旧M02A不挂Director时外层帧在撤权后不归零的真实RED。 |
| CharacterControlCoordinator.cs | 有，中文职责/边界/协作 | 在Root前更新；角色成员、初始索引、可配置SwitchAction保留。Current是唯一选择真值，inputs是其成员输入引用。；新增只读代际与全来源许可：启动配置/组件/焦点/应用暂停/有效Current/Root就绪/移交帧共同约束Player和AI。无第二个选择状态。 |
| CharacterInputHandler.cs | 有，中文职责/边界/协作 | 输入快照和所有Inspector Action保留，本轮只新增实时许可接口实现与准确协作注释。；原owner GUI阻挡区、玩家来源引用、受管理标记与松键武装保持原样；HasPlayerInput现在也由Selector通过窄接口只读消费。 |
| CharacterRoot.cs | 有，中文职责/边界/协作 | 最小公开接线：Root准备好后校验来源owner、绑定只读session与伙伴provider；Clear只由同owner清有效Frame，不改物理Held或动作身份。；在原具体input槽外构造单个Selector，把同一对象注入Bus；未绑定Director时委托旧玩家源。领域Runtime/Definition、权限与子系统初始化保持原顺序。 |
| CharacterIntentSelector.cs | 有，中文职责/边界/协作 | 依赖纯来源策略与Unity帧/对象生命周期，不采设备。；持有本角色、原玩家源、只读会话和伙伴源；managed表示曾绑定，不是isSelected。只有frame是Bus最终有效快照，kind是当前采样来源；帧号/代际用于失效，不是动作时钟。 |
| CompanionFollowDirector.cs | 有，中文职责/边界/协作 | 场景适配器依赖纯策略和原会话；readonly观察DTO只做开发显示，不推进策略。；Inspector coordinator是唯一权威来源，阈值为候选参数。entries只保存各成员策略历史/实际前位，不建立第二身份名单或Current。 |
| CompanionFollowDebugView.cs | 有，中文职责/边界/协作 | 登记/移除自己的GUI阻挡区域；缺成员/输入安全跳过，禁用不影响其它owner。；读取原Current、阈值、每角色EffectiveIntentKind与Director只读观察；按钮调用真实切控/禁启/重建策略/检查入口，不写角色位置、不显示假成功。 |

新Tests/Program.cs和各MCP .cs.txt均有中文用途与夹具边界说明；未改旧/第三方源码。不把当前注释或阈值建议登记成新的永久玩法规则。
