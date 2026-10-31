# CRAFT-01 pure core implementation plan

**Goal:** 按Root已批准冻结合同实现纯C#配方、稳定材料计划与内容指纹；不接Unity、库存发布、保存或UI。

**Architecture:** 不可变定义和来源快照；Planner只返回消费事实，不持有库存；Identity对长度前缀的固定顺序事实做SHA256。Root负责合法团队来源预过滤、工位资格、最终库存版本/容量复核和原子提交。

**Tech Stack:** C#7.3兼容源码、System.Collections.ObjectModel、System.Security.Cryptography；离线net9测试exe，无NuGet测试依赖。

**Spec:** 父任务2026-10-31消息中冻结签名，已由Root明确确认；整体架构见外部CRAFT-01/00-baseline/plan/03_根结构与库存事务约束.md。该子任务只拥有Core/与本CoreTests/，不Git/Unity/共享文件/asmdef。

1. [x] 测试先行：CoreContractTests.cs通过反射发现真实类型，因此空生产目录也能运行并断言“缺少类型”，不是编译错误。覆盖校验、冻结、稳定分配、预留/装备排除、批量/预算/溢出/缺料/重复来源、指纹字段与分隔歧义。
2. [x] dotnet run，保留RED实际输出，确认缺少功能断言失败（0通过/41失败）。
3. [x] CraftRecipe.cs仅负责不可变配方与目录；CraftMaterialPlan.cs仅负责快照/消费事实/计划；CraftPlanner.cs负责纯分配；CraftIdentity.cs负责canonical SHA256，所有类和复杂边界中文注释。
4. [x] 原套41测试GREEN；再补独立Python golden与边界复核，最终46通过。输出版本及验证限制给Root。

不提交由Root接手的任何文件，不运行全工程。生产缺失导致的RED和最终独立纯源码GREEN均不代替Unity集成验收。

后续Root委派已完成：

- [x] 回执Journal与Serializable字段DTO；先27条新增RED，再73GREEN；Guid空白边界真实RED→74GREEN。
- [x] Editor/CraftingAuthoring.cs作者校验、Inspector与显式空实例ID按钮；旁路真实Unity引用编译0警告/0错误，Native执行由Root验证。
- [x] 交接更新，全部子任务写入路径释放。
