# 配置入口与样板整理实施记录

- 任务ID：CONFIG-01；状态technical-complete；写入模式docs-writer；2026-10-24。
- 用户已指定v1.0详细方案并授权执行；沿用既有设计，不另开运行架构/模型/美术标准。以附件为规格，聊天链接未返回正文。
- 本机/远端main精确为0b922064b217e46e274ec29b5c2349ac355aa2e1；无基准分叉。35项既有dirty（31旧资源删除+四修改）保留，不暂存、不恢复。
- 当前唯一Unity writer；B综合场Edit ready/clean、无PrefabStage。A=65440/B=64480，view实际绑定各自MainBody；A/B Mapping独立，共用Router/Rig。
- 独占：Scripts/Characters/Editor/CharacterRootEditor.cs与CharacterAnimationNavigation.cs；Scripts/Inventory/Editor/CharacterContentWorkbench.cs、CharacterConfigurationCatalog.cs；docs/ossastra/ArtIntegrationCandidate（含Generated）；功能模块清单受影响M01-M04/M07/M11/M24；Candidates/CONFIG-01；本任务同步。新.meta由Unity导入生成。
- 不处理：所有Runtime执行代码、原SO结构/作者资产常值、旧参考、第三方、源骨架、外围系统、正式标准。B只用于正常样板和可撤回配置验证，结束恢复磁盘/资产；不新增永久场景。
- 项目/Unity MCP/brainstorming/writing-plans/TDD技能适用；已有用户详细批准设计，直接内联执行，不为同一已授权范围再设确认门，不派子代理。

## 设计与分步计划
- [x] A：实际view只读绑定快照+角色根Inspector。保留默认字段；显示场景覆盖/Prefab默认，四导航调用真实Selection/OpenAsset；Play只显示已保存运行结果，Edit不初始化。先证明缺入口，再验证A/B、Prefab、缺绑定、多选和面板无资产/场景改写。
- [x] B：补现有Workbench导出，schema3记录日期/基线/来源，savedscene绑定与默认对照、SO引用GUID/兼容分类、Freeze实际时点、Rig/grip/变换；保留模型完整目录，重导当前依赖。使用Editor API已开clean场/临时加法场只读导出，不改变用户选择或已开场。
- [x] C：更新README/readiness/受影响模块；四关系表+一页当前A/rozeul/既有主副手/身体技能样板，当前值/建议/用户确认分开。一次暂改mix保存回读并恢复、一次正常输入/换装/动画观察，B同构检查。
- [ ] 收尾：导出引用一致、准确字段/链接/无用户验收勾选；最小编译/现场/日志检查，定向GitHub发布核SHA，无Gitee/关机。

## 接口与验证安排
- Editor导航读Root.serialized.view，不按路径猜组件；没有绑定就报缺失。导航只切选择/打开资产，不调用Root Initialize/配置Resolve/播放。
- CurrentClip/CurrentSource/当前动作与composer已提交Intent身份用于Play读数；逻辑ID仅在已执行意图或其冻结计划中实际存在时展示，否则标未记录。
- 样板导出对savedscene及真实资源，静态默认计划与实际运行记录分开。旧animationProfile/animationKey为兼容，不再推荐。
- 验证证据放Candidates/CONFIG-01；保留初始只读基准、导航操作回读、临时修改恢复、正常小流程、导出一致性和最终现场。无需重跑AM全套合同。

## 已完成证据
- 新增只读根Inspector/导航；A/B四导航真实处理器Selection/OpenAsset回读13项；Prefab默认/缺view不猜绑定验证。Play读已有Controller/Composer事实，不解析规则冒充结果。
- 工作台schema3当前164SO/3模型755条，116局部PASS/48INFO/0REJECT；兼容引用和当前链分开。Unity当前无OpenPreviewScene公开API，改为已开clean目标场只读；目标dirty前置拒绝，未开时additive并finally关闭，不保存。
- 当前A Mapping mix .08→.12保存/重新导入回读并finally恢复，原文件与meta字节一致，B独立；5项通过。小流程虚拟设备输入+实际uGUI处理器/Raycast换装19步通过，包含当次旧快照/下次新mix、主手/Q/盾A/盾B。不是实体手柄验收；威胁隔离、体力恢复/靠箱测试传送明确记录。
- README替换陈旧作者链并给四关系表；readiness区分旧Player阻塞/正式美术/用户标准；M01–M04/M07/M11/M24仅更新事实，所有用户待审勾选保留。


- 额外导出日志缺陷已最小RED→GREEN：SO枚举排除.unity（仍保留Scene指纹）；修正后导出无ReadObjectThreaded。最终编译ready，单一B Edit clean，无PrefabStage；326依赖指纹/307对象GUID对账、72待审checkbox未改。详见ArtIntegrationCandidate/ConfigurationHandoff.md。Unity/源码租约释放，仅本轮定向文档与发布租约。
