# OSSASTRA COM-01：物品、交易与交互完整迁移

- 任务 ID：`com01-commerce-migration`
- 状态：`complete`，实现及技术验证完成；本记录随精确清单定向发布 GitHub。
- 日期：2026-10-28—2026-10-29 Asia/Shanghai。
- 执行：root 唯一 Unity writer；三个子代理分别负责纯规则、保存/发布保护和 UI/文档，现均已释放写权。
- 合同：用户授权 COM-01 完整计划包 I0—I6 连续实施，D01—D12仅为本轮 SAMPLE 默认；完成后让电脑睡眠。

## 已完成

1. 无 Unity 依赖的 Commerce Core，通过现有唯一 InventoryLedger 原子完成报价、实体币付款、交货/返款、独立商店存货、模拟补货与去重回执。没有第二钱包或第二背包。
2. 完整经济快照：唯一物品位置/数量、手槽装备 payload、耐久去重、冷却、能力资源、效果来源、商人/时钟、世界箱/掉落、优先/快捷引用、对话标记；恢复先完整校验，只有明确 Missing 才初授。
3. 冻结字节的串行保存器、原子替换/备份、代际 fence、旧写及旧预览拒绝、失败后最新内存重试。关闭在主线程捕获最新变更并同步排空/关闭；Inventory先失活也能捕获。
4. 同一既有女主的个人店与 SAMPLE A/B 对话/回访、独立 NpcRoot 普通商人、三列真实 uGUI、数量/报价/保存反馈。沿用原库存的拆合、取存全部、整理、后备给予/收回/优先、快捷、卸装/换装、丢弃/拾回。
5. 完整入口 `Assets/OSSASTRA/Scenes/SC_CommerceIntegration.unity`；`SC_CommerceTransit.unity`仅作跨图验证配套。两场均为 `commerce-main`、空 saveDirectory（默认 persistentDataPath/Commerce），没有本机测试绝对路径。
6. 三个作者模板 PF_CommerceService / PF_Merchant / PF_WorldContainer；明确配置稳定身份/目录后执行当前地图登记并保存。重复 ID 或缺目录先拒绝，不能偷偷重写已有存档身份。实际商人 Prefab 放入完整地图、登记、运行已证。
7. 世界交互标签设为 Navigation.None，防止距离拒绝后持续抢占 WASD；普通 Tab/Esc 与交易捕获分离；失焦闭窗、回焦 Game View 后重新采样、外来 owner 保留均已验证。
8. 禁用交易组件不再锁普通背包；保存失败仍锁主动编辑。禁用期间没有 Update 检查点，退出/销毁仍最终 Capture/Flush；显式移除模块不代表持续自动保存。

## 技术验证

- Core 45、原 Inventory 37、Persistence 41、Session 28、UI 22、实际生产 Codec 12，共185项分层测试通过；不重复累计旧测试组。
- RuntimeCompileCheck：0警告、0错误；实际 Editor 最终 prepare ready、编译无错误。Codec 四个实际程序集 SHA 已更新。
- 普通店实际 PlayerLoop/uGUI 买2：币300→280、A库存10→8且B仍10；指定同实例卖1：币→284；个人店买1：币→274。SAMPLE对话A/B三句、选择、末句与回访均有回执。
- 真拒写：确认按钮自动禁用；释放拒写并重试后原选择/数量保持、币不重复扣；保存失败期间受理的消费/死亡/全灭仍继续，隔离档重启保留。
- 双箱实际取空；箱A取存全部/整理；自然全灭包容量受限回收8项后4手槽原实例直回原手槽；没有新建装备。
- 实际 Flush 成功后 Single 主图→Transit→主图：35旧物品身份/数量/位置一致，新地图仅首次两箱新增2实例，总37；空箱、6商人、优先/快捷保持。
- 真正 Editor PID46240退出→60612：37物品、世界记录、对话标记、商人、装备稳定字段相同。耐久97/3wear/CD期限/4来源保持；5秒增强随新进程实际Play推进11.8149秒正常过期，没有按离线时长补算。
- 1440×900及实际1200×800渲染/事件回放；溢出数量拒绝，数量输入焦点内 F 不成交。外部 TimeScale0 只用于冻结验收时钟，未添加游戏暂停功能。
- 无 Commerce 的原 `SC_AUDIO_BB_Fusion`实际 Play：Inventory Ready/CanEdit、两个角色存活、原唯一音声根存在；原场未增加交易币初授。完整旧库存37项另已回归。
- Editor备份公开入口：明确选择13/14备份恢复，14/15原主档保留 `.corrupt-*`；下一次启动完整校验后 Ready。

装备特殊值、拒写期间消费/死亡及关闭顺序属于显式领域/宿主夹具；拖放属于真实 uGUI 事件回放。均未冒充完整物理手柄操作或自然施法。

## 最终现场及发布保护

- 主场 GUID `1bd6374d991c524479b0f9f523bd130d`；Transit GUID `4147556d4efaaa0458788b14e4dd900b`。
- 最终主场 Edit/ready/clean、QHD(index5)恢复、TimeScale1；本轮两个临时证明场已通过 Editor API 删除，原有场景删除现场没有动。
- 起始37项dirty逐项保护：36项指纹保持；Registry仅本任务租约文字变化且整文件不提交。两个旧dirty场、角色装备表现、DebugControls、InputLayout及31项原删除均不带入提交。
- 角色Root/控制器/输入仲裁/动作/Spine动画和音频算法未改。七个纯装备文件仅增加partial状态快照接线。
- 只暂存外部精确白名单中的本轮代码、meta、两场/配置/模板、BuildSettings、测试源码和文档。禁止全量Assets/Candidates暂存；不发布bin/obj/DLL、真实存档、截图/音视频、受限媒体或原作DLL。
- 目标仅 GitHub `huangjianhangxbx-star/OSSASTRA` 的 main，禁止Gitee；最终远端HEAD由推送后 `git ls-remote` 对账。

## 保留范围

原作没有安全隔离档，本轮未进入用户原作主档；原型自然路径21截图与固定代码参照已保留。未代签鸭科夫动态UI高度还原、实体手柄/物理拖拽手感、用户布局及架构审查。不是完整游戏存档、正式平衡、美术或Player发布构建认证；旧第三方Player编译问题独立保留，不扩大本轮范围。

## 持久交接

- 配置步骤与模块边界：`docs/ossastra/tasks/COM-01/配置与使用.md`。
- 分层结果、风险与保留项：`docs/ossastra/tasks/COM-01/验收矩阵.md`。
- 可发布脱敏证据：`docs/ossastra/tasks/COM-01/验收证据.json`。
- 本机原始回执、测试日志、隔离测试档和发布白名单：`E:/WORLDCREATOR/XingHaiHuiLang/work/OSSASTRA/COM01-20261028/`。不提交原始存档与媒体。
