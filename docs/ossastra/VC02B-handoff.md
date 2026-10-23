# VC02B 交接

VC02B B0–B8的包内Editor技术闭环完成，供用户进入正式美术与作者契约审查。正式美术未完成，用户标准未冻结；不是成品发行。

## 打开与操作

打开 `Assets/OSSASTRA/Scenes/SC_VC02B_IntegrationReady.unity`，进入Play。每个新场景会话有限授予31实例；普通返场或窗口重开不授予。

- WASD移动、Z切控；普通攻击/闪避/主副手能力、Q/R配置与固定E继续沿原HUD。初始技术蓝色Spine用于检验左右手独立跟随，非正式装备美术。
- Tab开包，开包仍可WASD；输入数量框聚焦时捕获移动输入。点击真实物品才显示详情。拖入合法格/手槽，Ctrl快转；拆分数量位于详情。近箱F打开；箱内容可滚动，包含食品/收藏/魔素/有限装备。
- 在合法场景落点松开拖拽丢物；F拾取或开包拖地面物品入包。满包提示且保留原地物品。
- 详情交付/收回女主五格后备并设优先；非实控合法用药/投弹，接管后不新发托管请求。1–0仅引用/选择，不是新增战斗自动施放。
- 同场全灭后普通库存/装备/后备进入一个掉落包，安全栏保留；返场空手，拾回原实例装备。Y归原共享瓶服务；附近篝火F按原规则补瓶。

## 建议审查顺序

1. `VC02B-migration-coverage.md`：原型与本候选的范围、初始配置及明确差异。
2. `ArtIntegrationCandidate/README.md`：真实Rig、空间/握持/生命周期、SO状态所有权、四套已运行配方、规则分层。
3. 菜单 `XingHai > OSSASTRA > Character Content Workbench`：点SO检查与逐引用定位；显式选择本体观察挂点。使用预览会消费当前测试会话真物品。
4. `ArtIntegrationCandidate/Generated`：157 SO字段/引用、3模型755行完整目录、时线、指纹。目录不是空模板，也没有给每个装饰骨臆造用途。
5. `VC02B-verification.md` 与 `ArtIntegrationCandidate/readiness.md`：24合同、E01–E12、自然/边界证据、失败与未测边界。

代码阅读入口：`Scripts/Characters/Core/Inventory/InventoryLedger`唯一位置/数量；`EquipmentItemState`稳定payload；`InventorySession`同场权限和装配；`InventoryItemUse`接受预留/释放消费；`CompanionInventoryConsumer`Root采样；`CharacterAbilities/AbilityRun`既有能力事务；`CharacterRigProfileSO/CharacterEquipmentSpineView/TransientItemView`纯表现；`InventoryView/InventoryHudBridge`视图与输入。没有在Root按具体装备名新增玩法分支。

## 重要边界

Editor自然故事47/60/35、UI57、两盾10、作者6、Spine24+20、域重载13、A纯127/A自然56/旧场15通过；详见原始证据。213个A原生资源SHA不变。全部结论基于本机含四项保护dirty的工作区，不宣称隔离干净克隆。

Windows开发构建追加验证失败，原因是既有Unity-Favorites-Tool编辑器代码进入Player程序集；未改第三方插件。Editor候选可以审查，独立Player尚未验证通过。

保留且不提交：用户SC_CharacterVerticalSlice命中层、CharacterEquipmentView行尾、旧禁编asmdef/meta删除。不改源商业媒体、人工规则、原型或Gitee。用户已取消关机，不执行。GitHub提交及远端核对结果见本轮publication记录。
