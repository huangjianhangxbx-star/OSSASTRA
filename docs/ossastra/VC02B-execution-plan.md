# VC02B 执行细化
采用已批准02计划与03矩阵，原型冻结c21d4fc。连续执行，不新增阶段审批。
## 分解与接缝
1. Characters/Core/Inventory：纯定义快照、唯一实例、位置、修订、事务、数量预留、有限整理、全灭批次。装备payload复用EquipmentAbilityState，通过只读槽位接口接EquipmentState，不双写。
2. Scripts/Inventory：SO目录、场景唯一InventorySession、装备绑定适配、访问/交互、道具来源及伙伴消费者。能力继续原接受/释放，投递继续A队列，恢复继续CombatRecovery。
3. Scripts/UI：正式uGUI背包/详情/外部/安全/后备与事件拖放；仅捕获战斗，移动许可单独处理，F统一上下文。
4. VC02B自有资产及SC_VC02B_IntegrationReady：Editor API复制已保存A场，仅修改候选；初值只在会话创建授予。
5. 原独立装备Spine端口扩展左右手与显式RigProfile，作者Inspector/目录导出；不改共享SkeletonData。
6. Candidates/VC02B：纯模型红绿→装备世界→真实输入UI→消费者/AI→掉落自然故事→Spine生命周期→旧受影响场回归。
7. docs/ossastra/ArtIntegrationCandidate：实际目录/引用/动作/配方/标准边界；逐合同证据再发布GitHub。
## 关口
按B0-B8执行；24合同逐项登记，不用模块单测代替自然输入、不用A技术盾证明正式美术完成。保留原dirty四项。范围止于同场核心，不加入商人/家园/存档。
