# M01-R1 验证记录

适用版本：本机候选相对6fdd36a；精确生产源码SHA/行号见M01-R1-source-manifest.json。团结2022.3.62t16；自有SC_M01_CharacterTest / PlayerCharacter.prefab。

| 项目 | 实际执行方式 | 最终结果 | 边界 |
|---|---|---|---|
| 领域核心 | dotnet run --project Candidates/M01/Tests/Ossastra.CharacterCore.Tests.csproj -c Release | 29/29 | 本轮核心源未修改，重新运行，无外部测试包 |
| 输入配置/隔离 | Unity execute_code运行InputBehaviorContract.cs.txt | 10/10 | 真InputAction+虚拟键鼠手柄，临时设置/设备finally清理 |
| Bus/朝向旧合同 | Unity运行FoundationContract.cs.txt | 12/12 | Direction/FacingX改为AttackDirection/VisualFacingX；显式FaceAttackDirection适配分离语义，保留原隔离/时钟/快照检查 |
| 原Play闭环 | baseline14.cs.txt，44定时步骤 | 14/14 | 更新Development.DebugCommand调用与自有指针绑定，无并发Unity工具占用主线程的最终轮 |
| R17真实世界 | r17-world-contract.cs.txt，39定时步骤 | 15/15 | 真Root/InputAction/Spine/Physics/左右靶Health；虚拟设备，非物理手工操作 |
| R18替换边界 | interface-contract.cs.txt | 12/12 | 替换输入/运动/朝向/表现/命中实现，实际Root初始化函数，失败停止、Release快照与原Health去重 |
| 两个同构角色 | two-actor-contract.cs.txt | 4/4 | 两份真实共享Prefab，激活前不同actorId，实际Awake/Bus/owner/朝向隔离，结束销毁 |
| Unity编译 | prepare_editor + get_compilation_errors | 0错误 | 没有声称旧项目所有场景/Console或Player构建均通过 |
| 人工手感/逐行审查 | 用户实际键盘鼠标与源码核查 | 待用户 | 不用虚拟设备或AI判断代签 |

## 失败与修正原件

- 首次R17探针未隔离实际键盘来源，未观测到期望移动，不能算RED。收紧后真实Frame.move=(-1,0)、鼠标右、角色左移而Spine.ScaleX=+1，明确RED；原件均保留。
- 新接口替换探针最初受已解禁旧global::CharacterDataBus同名遮蔽，编译失败；使用全限定新类型后通过。这是探针命名问题，非生产脚本编译错误。
- 自审发现HitResolver改收完整释放参数后不再需要Bus；新增无假初始化义务检查先失败，再移除其Initialize，最终12/12，未强迫所有模块实现初始化接口。
- 最后基线重跑中并发执行另一个Unity探针占用主线程，早期排队输入和检查落在同一帧，2项失败。保留该轮；不改游戏逻辑，无并发阻塞重跑14/14。
- execute_code跨域重载出现Request cancelled时，先读取实际Prefab/场景结果；确认未写后才继续，未盲目重放。prepare_editor均按原request_key读取恢复。

## 试玩和复现

打开Assets/OSSASTRA/M01/SC_M01_CharacterTest.unity，Play并点击Game View。
A/D移动，鼠标放相反方向应跑步朝移动侧；松开移动后左键普攻应打鼠标侧靶。按住不连续重放。X取消，P暂停，F1–F4为Development适配器的真实开发入口。
自动化.cs.txt由已安装Funplay Unity MCP execute_code在ready Edit或指定Play状态运行；不是Unity Test Runner注册测试。完整命令源码和原始回执随外部审查包提供。

## 保护与未测

本轮开始时只有自有测试场处于Play/clean，无PrefabStage；未运行旧场。旧卧室、参考VELGITA、target 1.fbx、rozeul_SkeletonData.asset四个文件前后SHA一致。没有改旧代码禁编状态、原Git、体素比例、Spine媒体或URP。
对应旧素材.meta本轮没有全部单独预留前指纹，未声称完成全项目/meta严格零差异；自有新组件.meta和原引用由Unity生成/保持。没有全项目旧场、所有地图、低端性能、Player构建或干净克隆验证。
用户明确回复没有编辑、允许清理保存后，仅经Unity Editor API移除1条失效debugTarget override并保存自有测试场。回读旧覆盖0、缺失脚本0、6个Root引用与Development目标引用齐全、dirty=False、Probe设备0；cleanup-final.json留存。这次仅清理无效序列化覆盖，未改测试通过的生产源码。

