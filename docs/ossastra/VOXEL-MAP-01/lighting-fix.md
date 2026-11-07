# VOXEL-MAP-01 统一受光漏接修复

用户报告同一体素受光面板仅部分模型有效。实际主场Comparison仍只有27个旧绑定，新安装根247个MeshRenderer覆盖0：地形224、木屋8、树12、水1及旧高台新顶面2。新材质虽然是VoxelLit，却只烘了固定响应，因此没有接入实时面板。

另外，简单追加绑定仍不充分：当前完整Look对未映射源会回落原C_live；地形又依赖vertexColors=true，而共同Profile为false，直接覆写会丢土侧/洼底固有色。Native临时夹具已实际RED：source vertex1、预览vertex0。

## 修复

- 既有Comparison增加`Binding.lightingOnly`，旧27默认false保持原行为；新247为true，A精确归还原材质，B/C跟随统一响应，包括当前ActiveLook及两滑条。
- 该策略保留自身表面配色、纹理/PBR和顶点色用途，不借用旧模型候选的配色映射。缓存键包含策略，避免同source/profile的保色与换色组串用；仍执行原材质所有权让权/归还规则。
- 面板分别显示统一受光274、保留配色247、当前候选实际换色19，不把配色映射数当受光覆盖数。退出Play待固化默认的绑定签名包括策略。
- 显式Editor登记入口`VoxelTerrainLightingAuthoring.Register(installation,comparison)`只处理同场景、明确安装根的持久VoxelLit完整槽，原旧Binding对象与顺序保持、重复登记返回0、失败先校验再修改。未来同一Installer调用此入口，不靠逐帧扫场补漏。
- 主场与独立场均保存重开确认274/247、所有原作者槽精确恢复。原作者启动A/defaultLook空保持，不擅自固化用户当前临时C预览。

## 证据与交付状态

登记合同7项、双策略共享源缓存合同4项、实际主场Play集成合同9项全部通过；包含ActiveLook当前旋钮覆盖新所有槽、A/B/C、顶点色/表面不变、源/Profile不变、重复刷新材质身份稳定、同源仅26个私有材质共享、完整Look导出及测试后还原。

实际同机位C亮面0%与50%游戏图已观察，新屋/地表/水等共同变化。合法绘制Active、Unsupported=0、Error空，缓存冲突0；清理后编译/Console无新错误。测试前对Store做独立隔离，结束后原4业务文件SHA不变；原作者路由已恢复。

最后回到正常主场Play（非测试Store），恢复用户原预览：浓厚暖砂、亮面15%、背光0%，其余会话参数按开始前快照恢复，面板展开。未固化新默认、未修改2D/灯光/天气/相机/碰撞或Shader数学；无关原dirty保留，无电源动作。

本轮未重做Windows整包运行验证；既有构建/Player候选已把过期27固定守门更新为27旧+247统一受光，并各自离线编译0警告0错误。上轮138帧Player是旧覆盖范围的历史记录，不冒充本轮结果。

本机证据：外部`研究/VOXEL-MAP-01-LIGHT-FIX/{p0-live-snapshot.json,p0-session.json,integration-green.json}`及项目`artifacts/VOXEL-MAP-01-LIGHT-FIX/{c-zero.png,c-lit-max.png,restored-user-preview.png}`。源码/合同/本说明按R11定向发布，作者场景、材质和截图保留本机。
