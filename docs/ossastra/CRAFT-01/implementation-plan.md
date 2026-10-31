# CRAFT-01 实施计划与基线

基准8c419d8639c67549eb97cd4d58f2c6f12dbf575d。用户明确执行附件00—06；聊天链接无可读取正文，不能冒称读到。默认数值与规则是本轮SAMPLE，未写为永久设计细则。

1. M0：保留原37项dirty差异与默认commerce-main四文件指纹；原WorldPropsIntegration当前dirty通过Native saveAsCopy留在Crafting/PrivateCheckpoints，源场不保存。所有Play前必须确认唯一新CRAFT场、GUID、无Prefab Stage、playModeStartScene及全部保存宿主的隔离绝对目录/profile；失败不进入Play。
2. M1/M2：两原作配方、科技/设备、材料、装备payload及存取链源码研究，逐项标明源码证据/实际运行未测。只提取选定媒体，清单保留SHA与来源。Main/Transit复制磁盘样板，每场3个、共6个Map各重新登记，旧Map/源场SHA复核。
3. M3：纯Core定义/稳定分料/溢出/指纹先RED→GREEN。库存新增opaque PreparedInventoryConversion，不公开内部EconomicBatch，不伪造商人；候选先扣普通team材料，再放装备或中间品，以原布局验证最终容量，最后一次发布Ledger与预分配receipt引用，再通知。
4. Root在Awake登记原Commerce共同快照参与者；同一全局配方/物品/工位定义目录在Main/Transit一致，旧档缺Crafting section严格拒绝。所有产出使用新独立EquipmentItemState及默认AbilityState，绝不复制角色当前payload或自动装备。
5. 单一InventoryInteraction继续持有F：散落物优先，其后工位/商人/容器/篝火距离与稳定tie；已开工位优先消费确认。CraftingUIRoot复用原库存capture owner/uGUI/EventSystem，不暂停、不改Q/E/Z，打开帧吞确认，编辑数字不误成交。
6. M4：配方SO、工位SO/实例、原材与成品目录、至少主手+副手+中间品。工位固定不可破坏；同定义两稳定ID和一个拒绝样板。实际uGUI两列配方/材料/数量/确认/理由，原背包装备和真实使用。
7. M5：confirm要求当前服务签发preview，包含actor/control/session/station/map/recipe version/revision/generation/来源分料指纹；旧预览任何变化都拒绝。receipt有界保留，已过期服务票据不能重新准入；同request+同指纹只返回旧receipt，换内容拒绝。save失败只重试保存，跨场Flush失败不卸载。
8. M6/M7：自然采集拾取→制作→装备→使用→保存→跨场→冷读；异常/满包/扣料腾格/预留/禁用与超距LOS/旧预览/观察者抛错等定向验证。录像、中文教程、实际测试矩阵/限制、最小回归；只定向提交本轮路径与共享文件增量，保留原dirty，GitHub R11发布。

## 接口与分工

- Core：CraftRecipe/CraftCatalog/CraftPlanner/CraftIdentity，不依赖Unity。craft_ds_research负责独立Core与CoreTests。
- 库存：Ledger.PrepareConversion/CommitConversion和Session.PrepareTeamConversion/CommitTeamConversion；craft_inventory_audit仅两个新partial与专用测试目录。
- UI：ICraftingPresentationHost、CraftingUIRoot；craft_kt_research只Presentation目录。
- Root：SO、CraftingRoot/Station/同快照receipt、authoring工具、唯一F接线、Native场景/素材/真实验证，唯一Unity writer。
- receipt策略：最多保留128条最近成功事实；未命中receipt的请求必须与当前服务签发的唯一preview一致。每次成功或依赖变化立刻撤销旧票据，重开/读档不复用会话或票据；清理旧receipt不会令任意旧ID重新可执行。

## 验证命令

纯Core与库存分别`dotnet run --project Candidates/CRAFT-01/CoreTests/...csproj`、`.../InventoryConversionTests/...csproj`（实际文件名按实现记录）。原库回归限受影响的COM01/SessionTests和WORLD-PROP-01/InventorySeams，不泛跑历史。Unity编译由prepare_editor(edit)与console核对，Play独立硬门后实际输入及uGUI业务断言；真实硬件/Player/GPU与原作动态未测不宣称通过。
