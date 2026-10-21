# ROADMAP02 真实素材核验
2026-10-21 Unity AssetDatabase窄查询Assets/随便尝试：
- rozeul_SkeletonData.asset，GUID 8de59581980c83e4e91f8a7e5bc92f46，Spine4.3.26，实际GetSkeletonData成功；idle1_1/run/atk1/atk2/upper_hitted/dizzy_loop/death均存在。本轮新场映射它们；原资产未改。
- New Folder 1/Guinevere_SkeletonData.asset，GUID 074a9931b83c7d74ca734374d58499ce，Spine4.3.23，加载成功，仅idle_01/Idle_Stand/Idle_Start/Touch1/Touch2；本轮未使用。
- 现有玩家视觉Default sorting layer；原visual保留，敌/NPC复制视觉实例为TEMP_2D_VISUAL，并移除玩家CharacterSpineView，接中立播放适配器。无玩家输入/Root/HP复制。
- 新场敌、NPC本地采用同一现有角色作技术替身，敌可视高度约1.6单位，3D CharacterController照旧。敌旧胶囊Renderer关闭，碰撞保留。
- 正式敌人素材/公开发布权未经核实：ASSET_BLOCKED。技术本地Spine加载与2D接入PASS不等于正式敌人迁移；不上传atlas/png/skel/商用库或含媒体截图，仅场景引用和自有代码。
