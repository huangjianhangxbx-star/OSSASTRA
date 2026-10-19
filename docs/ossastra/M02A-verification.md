# M02-A 实际验证记录

版本：本机P0基准4eca664加M02A候选；最终源码指纹M02A-source-manifest.json。Tuanjie2022.3.62t16、InputSystem本机包、Spine4.3.26。所有数值为实际运行回执；自动通过不等于用户逐行批准。

| 范围 | 执行与证据 | 结果 / 边界 |
|---|---|---|
| P0中文补齐 | p0.cjs移除注释/空白比较、comment-token-check.json | 16/16词元等价；当时29核心+10输入通过，编译0 |
| 核心 | dotnet run --project Candidates/M01/Tests/Ossastra.CharacterCore.Tests.csproj -c Release | 本轮最终29/29；没有重复HP/时钟 |
| 输入与配置 | InputBehaviorContract.cs.txt | 最终10/10；Edit预览场、真实InputAction和虚拟键鼠手柄 |
| Bus/原所有者 | FoundationContract.cs.txt | 最终12/12；原权限/时钟/快照/双实例隔离 |
| 双角色实际闭环 | Candidates/M02A/Tests/world-contract.cs.txt | 最终26/26、83步骤、14.65秒；真Root/InputAction/Spine/Physics/靶HP、虚拟键鼠 |
| 配置与生命周期边界 | boundary-contract.cs.txt | 15/15；空/缺引用/重ID/重复成员/越界/无切换绑定、同帧移交、provider销毁 |
| 重复owner | conflict-contract.cs.txt | 1/1；实际D快照(1,0)不被拒绝的第二协调者清掉 |
| 单角色R17回归 | r17-world-contract.cs.txt | 最终15/15，正确M01单角色场；左右反向显示/实命中、非法指针/手柄回退、UI Held、粗帧单次释放、暂停 |
| 保存重开 | scene-reopen-approved.json | M02A已保存场两Root、12引用、0MissingScript、clean；再次返回单角色场做R17 |
| 可视检查 | artifacts/ossastra-m02a/dual-control.png | 实际Play截图已查看，双角色/双开发靶/实控与各自状态面板可见；相机与地块沿源场 |
| 最后编译/现场 | 最终prepare_editor和audit回执 | 见交付末尾现场；不声称所有旧Console/旧场景/Player构建通过 |
| 用户人工操作/架构 | 物理键鼠、实际手柄与源码逐行 | 待用户；程序性输入和生命周期回调不能代签 |

## 失败原件与修正

- 双控RED：现有共享Prefab两个真实实体读取同D输入，A/B各移动1.50137854单位，预期仅A移动失败；未用缺类型的编译失败冒充行为RED。
- 第二协调者RED：拒绝绑定后OnDisable仍Reset所有输入，把原owner真实D快照(1,0)清零。增加bindingsOwned保护，只允许取得绑定的实例清理，重跑原测试前后均(1,0)、拒绝状态保留。最终双角色26项在该修复后重跑通过。
- 场景作者命令初次少了InputSystem扩展方法using，Roslyn编译失败未执行任何场景写入；补using后成功，仅此一次执行。保留初次回执。
- 输入10项旧夹具在启用后排队一次松键但没有调用Read去采样，随后立即排队按下，新的移交武装规则看不到松键，最后一项失败。夹具明确增加释放帧的Read，10/10；这对应真实Root每帧采样，不删产品松键保护，原失败保留。
- 第一次单角色R17回归错误地留在双角色场，FindObject取到未受控实例，9项失败。此前重新开场命令因dirty前提拒绝，后续曾未及时阻止测试；现已明确记录并补单角色数量前提。用户确认未编辑、允许重新开盘后，重开验证双场再进入正确M01单角色场，完整15/15通过；不以该错误轮的部分通过作证。

## 测试环境与保护

Funplay MCP execute_code命令脚本不是Unity Test Runner注册测试。定时Play夹具运行中不并发阻塞Unity主线程。虚拟设备、binding overrides和InputSettings在finally/清理中恢复；测试用生命周期SendMessage标为程序性注入，不声称真实切换窗口/键鼠操作。
本轮断电后先核对HEAD/远端仍6fdd36a，工作树与规范已保留，恢复新Editor会话后继续；P0先定向发布4eca664远端核对，再写M02A。
本轮没有覆盖旧场景、VELGITA、Spine媒体、target 1模型、URP或用户维护区；M02A相对P0的M01场与共享Prefab磁盘diff为空。旧禁编asmdef/meta的本机删除保留未入提交。未执行全项目/所有旧素材meta冻结审计，不声称全树无用户改动。

最终现场：SC_M02A_DualControl Edit ready/clean，ActorId temporary-a / temporary-b、12角色引用和2开发目标齐；0 MissingScript、0 Probe设备，原InputSystem.inputsettings.asset动态更新已恢复，编译错误0。最后仅补Root/Input/Movement/Spine的关键中文解释，产品逻辑不变；重新编译通过，源码清单按最终原文件生成。完整原始回执已定向入库docs/ossastra/evidence/M02A，失败轮不删除。

用户所给ChatGPT链接本轮通过read_thread只能取得较旧归档摘要和content-reference，无法取得完整较新正文。已完整读取v0.6附件00–05及README，实施依据是当前用户执行指令、附件和本机真实代码；未声称已读不可见正文。
