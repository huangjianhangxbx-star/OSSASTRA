# M02-C 候选发布

2026-10-20，实现提交704a16d9d4dab1a97aa2c022c7c1ad6b6dfe346a已定向推送GitHub OSSASTRA/main，原生git ls-remote核对一致。基准8925b76，51文件为本轮自有源码、独立场景引用、测试、证据与审查文档。随后本发布记录与计划勾选单独提交，不改变已验证源码。

未提交旧代码解禁产生的asmdef/meta删除，未上传bin/obj、第三方插件、用户媒体或旧Git历史，无Gitee写入、无强推。Unity自动序列化空字段的末尾空格保留；生产C#/文档/csproj差异检查已完成。

试玩：本机打开Assets/OSSASTRA/M02C/SC_M02C_FollowControl.unity并Play，WASD移动，Tab切换；原InputAction攻击保持。跟随是直接追随，遇墙停止；受阻后领队显著移动才重试。当前Edit clean、无缺失脚本和Probe残留。

审查入口M02C-review.md，完整9份变化源码/行号/SHA在M02C-source-review.md及manifest，验证在M02C-verification.md。测试证据不是人工手感或架构批准。所有审查条目仍待用户，停止M02-C，不自动进入M02-B/M03或战斗AI。公开快照仍缺用户/商用依赖，不能保证干净克隆直接运行。