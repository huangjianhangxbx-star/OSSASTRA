# ROADMAP03 验证

## S9：TECH_VERIFIED
- RED：旧真实Resolve扣HP至90，无法表达Guarded零伤；记录S9-red.txt。
- GREEN：dotnet Defense.Tests 10/10；原核心29/29、阵营伤害13/13。
- Unity真实OverlapSphere/同Release：6/6，Guarded HP不变/姿态半损/一次回执、Evaded双免、Normal原结果；防御为临时测试适配器，不宣称正式盾/闪避已完成。
- 新复制场真实InputSystem两段/切控/墙/破势14/14；旧B2场真实世界22/22。
- 编译先暴露CharacterCore→CombatCore不合法依赖，已改为同核心DefenseSnapshot；随后遗漏一处限定命名导致第二次错误，已修复并刷新ready。上述失败不是产品通过证据。
- 旧场/共享Prefab未保存，当前新ROADMAP03 Edit clean。原InputSettings由夹具恢复，临时设备/防御适配器已清除。
- S10-S13未开始；手感/架构人工审查待用户，正式敌美术仍ASSET_BLOCKED。

## S10 阶段验证（旧场回归待收口）
纯体力/闪避18；新场InputSystem与Physics综合18；两段连段/Stage2付款15。综合涵盖实墙、窗口内明确Evaded回执、窗外真实扣HP、AI独立费用、Current持键交接、10秒返场原体力补满。首轮死敌导致窗外检查失败已保留；第二轮AI激活点击落入既有开发UI捕获区，调整夹具交战位置后全绿，不修改业务门。

## S10 收口阻塞（2026-10-21）
- 已实测：纯18/18、真实Shift/物理/AI/死亡返场综合18/18、真实两段与Stage2双收费15/15、旧ROADMAP02连段14/14。测试结果均已落盘。
- 阻塞：旧场退出Play时prepare_editor未成功登记；直接查询得OPERATION_JOURNAL_UNAVAILABLE，editor-operations.json.tmp Sharing violation。菜单退出Play成功，get_editor_state Edit ready；一次干净Edit域重载后相同request_key仍无法登记。不是编译失败，不能把旧ready操作当成本轮prepare成功。
- 当前停在工具恢复关口，S10不发布，S11/S12/S13 NOT STARTED；恢复MCP日志可写并完成prepare/readback再发布，不跳阶段。
- 原型开发细则实际为v2.2登记草稿（含§7.1.1），附件提及v2.1属较早版本。未修改细则/人工区。
- 已冻结跟踪资产119项SHA无变化，旧禁编两删除保持。未为解决工具日志问题改Library、插件、用户场景或进程。
- 新场新增能力均已保存。当前回归旧场Edit clean，测试虚拟设备与临时InputSettings已由夹具清理。资源/方向缺口不伪装通过。

2026-10-21用户重启后恢复：prepare_editor操作d2d83875befd4617b392e8ff2f9b0a0b ready/Edit，journal_error=null。119保护项保持；真实输入设置与设备恢复。S10验证已收口，TECH_VERIFIED，允许按原计划接S11。

## S11 gate（2026-10-21）
独立左右手实例、空闲换装、方向盾防已实现。纯逻辑11项通过；真实RMB输入10项通过，含正面零HP/半架势、背面正常受伤、批次去重耐久、耗尽松键重武装、切人清理。证据 Candidates/ROADMAP03/Evidence/S11-world.json。仅新场装配RMB与装备，旧场未改。S12炸弹仍待实现，当前Bomb仅为装备占位。
