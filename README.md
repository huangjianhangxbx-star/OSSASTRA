# OSSASTRA

《星骸回廊》受控迁移工程的版本管理起点。

本次 PRE-00 仅完成本机工程隔离与技术空场景验证，不包含可玩的游戏系统，也不代表 M01 已实施。公开首发候选是技术骨架，不是本机完整团结工程的备份。

- 编辑器：团结 2022.3.62t16 / 1.10.4。
- 技术验证场景：Assets/OSSASTRA/Scenes/SC_PRE00_Recheck.unity，仅正交相机。
- 旧参考代码仅保留在本机，不进入此仓库。专用 asmdef 只有显式定义 OSSASTRA_LEGACY_COMPILE_OPT_IN 时才可能编译旧目录；正常保持该符号未定义。
- 本机商业插件、渲染实验、个人场景和模型不随仓库分发。

依赖及重建限制见 [DEPENDENCIES.md](DEPENDENCIES.md)，实际验收边界见 [PRE00.md](docs/ossastra/PRE00.md)。本机既有旧渲染配置与新技术场景应区别验收。

## 克隆限制

首发不包含 Packages/manifest.json、packages-lock.json 或旧 Graphics/Quality/BuildSettings；这些本机配置包含旧资产引用和本地工具依赖，不宜直接公开。干净克隆尚未编译验证，也不能保证直接用编辑器打开即可运行。可在相同团结版本的新建空工程中导入此技术场景；这仅是重建建议，尚未实测。不要为重建本机环境擅自复制商业插件。
