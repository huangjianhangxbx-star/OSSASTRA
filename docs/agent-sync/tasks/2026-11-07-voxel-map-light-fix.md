# VOXEL-MAP-01-LIGHT-FIX

- 状态：technical-complete
- 写入模式：unity-writer=root；code-writer子租约见下
- 请求：修复新旧模型只有部分响应体素受光面板。
- Root独占：WorldRendering/VoxelSurface/Runtime/VoxelSurfaceComparison.cs、Editor/VoxelSurfaceProfileSave.cs；Development/VoxelSurface/VoxelSurfacePanel.cs；Candidates/VOXEL-MAP-01-LIGHT-FIX；原InstallTerrainStage候选的明确接线调用；本记录/定向交付文档；主场与独立场的Comparison明确绑定。
- 子租约 voxel_map_arch_audit：仅 Scripts/VoxelTerrain/Editor/VoxelTerrainLightingAuthoring.cs 与 Candidates/VOXEL-MAP-01-LIGHT-FIX 下其Native合同。不操作Unity/Git。

沿用项目记忆、故障诊断、已有设计与TDD。同步四文档完整复核无重叠；42项原tracked dirty保留。根因已Native确认：原27绑定，新247对象覆盖0；临时比较组件实测源vertex=1，被Profile覆盖成0，RED。当前Play模式C/浓厚暖砂/亮面15%背光0已只读备份至外部研究/VOXEL-MAP-01-LIGHT-FIX/p0-live-snapshot.json与p0-session.json，后续恢复此预览，不固化新默认。

有限修复：给新明确对象增加lightingOnly策略，C读取当前会话包括activeLook，B统一响应，A归还；保留来源颜色/纹理/vertex用途，缓存按策略区分。原候选配色映射不自动泛化至新模型；不改Shader数学、灯/环境、2D材质、角色、业务、碰撞或相机。Editor注册仅处理显式安装根，保证幂等及完整持久化原槽；未来Installer复用该入口。

验证：先RED再代码与Native合同；两场保存重开274总/247新接入；主场C旋钮/原候选/AB、完整Look导出及source不变/旧27行为；同机位前后画面、实际合法绘制和无错误。Play测试前隔离业务Store；原档不回滚。R11仅定向自有源码/文档，保留本地资源。用户已取消关机，本轮无电源动作。

Root窄追加租约：Runtime/VoxelSurfaceLookSession.cs，仅使候选换色计数排除lightingOnly，并允许该显式组使用合法Look响应；不改旧槽映射/回退语义。

Root追加同任务候选租约：Candidates/VOXEL-MAP-01/Terrain/Player/{BuildSavedMain.cs.txt,VoxelMapPlayerProbe.cs.txt,README.md}，仅把已过期27固定守门更新为27旧+247新照明登记，保持原模型/隔离验证；本轮不重新做整包构建。

## 验证结果与交付
7项登记+4项缓存+9项实际Play合同通过；两场274/247保存重开通过，source/Profile与土侧顶点色保持。主场合法帧Active/Error空/Unsupported0。隔离测试后原4业务文件SHA不变；已恢复原作者路由并进入正常Play恢复用户Rich/15%-0预览，不改默认。源码/文档定向发布收尾，Unity/子租约技术工作结束。详docs/ossastra/VOXEL-MAP-01/lighting-fix.md。
