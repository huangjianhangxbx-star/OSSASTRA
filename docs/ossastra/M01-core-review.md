# M01 当前核心审查状态
核心源码已从Candidates/M01/Core迁到Assets/OSSASTRA/CharacterCore，作为唯一生产权威。Candidates中的netstandard2.1项目通过Compile链接读取这些相同源文件；没有双份运行时。原先“未导入Unity/无正式测试场”的描述仅属于第一批历史，已由本轮场景接入取代。

当前试玩、实际接线、验证与限制见M01-scene-review.md。核心6文件原实现保持，Unity真实程序集含这6文件，旧代码仍禁编；29项核心测试通过。全部核心源码、Runtime适配器和测试程序仍用户待审，架构未批准；用户已于2026-10-19授权候选推送，见M01-HANDOFF.md。

历史0/26 RED、26/27帧边界RED、27/28假Ready RED与最终29/29及两个隔离负控制原始证据保留于work/OSSASTRA/M01-v0.4-20261018；旧首批审查ZIP是历史版本，不替代本次新路径与接线审查。
