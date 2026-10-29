# 可选双角色试玩接线

生产真相是root集成后的`GroundWorkshopPlaygroundBuilder.cs`、`GroundWorkshopPlaygroundSession.cs`；这里的draft仅作交接，不能作为第二套生产实现维护。

两文件引用本项目角色、MapResearch与SpineDepth，必须从纯工坊插件包排除。公共窗口只依赖`GroundWorkshopPlayHooks`；未安装此适配时可以继续原创地图制作。

## 入口

- `Tools/OSSASTRA/地表工坊/打开双角色工坊场`：首次另存已有Frontier源场，包装七层配方并登记隔离资源；已有工坊场只打开，不覆盖。
- `Tools/OSSASTRA/地表工坊/用当前地图进入角色试玩`：完成当前笔画并保存作者图，再用原双角色链在临时副本试玩当前Tilemap与装饰。
- `Tools/OSSASTRA/地表工坊/返回作者地图`：有会话时退出Play；正常退出自动恢复原SceneSetup及地图选择。

默认敌人攻击关闭；真实命中回归由root显式启用已有入口，不新增控制器或扣血路径。

## 持久化与失败边界

临时输出只在`GroundWorkshop/Maps/PreviewSessions/<本次GUID>/`，包括World.prefab、Playground.unity、Session.json。会话通过SessionState跨Domain保留原SceneSetup/mapId、临时GUID及PlayStartScene快照。

地图根快照在自有短生命周期Scene生成，去除引用外部角色的GroundResearchView。临时模板装配时保留原输入/角色/镜头桥，复制观察面板配置并换到新的Surface。临时地图保留源注册身份，因此作者入口拒绝在试玩副本写入。

正常返回后，只有目录身份、三文件GUID、marker哈希均匹配且没有未知新增内容才精确清理。其他dirty场、用户切场、未命名Scene、未知文件或准备失败会保留检查点并明确报告，不静默覆盖/递归删除。

v1试玩要求顶层地图根、水平非剪切矩形XZ范围；未实现任意高度/剪切世界。角色尺寸和组件不变，只在新副本范围内设置出生点。Collider为连续3D地面与四边墙，不从装饰alpha推断碰撞。

## 验证

`PlaygroundAvailabilityContracts.cs.txt`已在实现前由root观察缺类/Hook/菜单的真实RED。集成后必须验证GREEN、新七层ReferenceLab、三层原创地图两次真实入Play/退出恢复、作者哈希、临时输出清理、保护旧场SHA与原控制链。草稿存在不代表这些Unity验证已通过。
