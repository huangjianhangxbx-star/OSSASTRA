# v1.2 显式地表颜色烘焙作者入口

仅候选，Root 负责导入及 Unity 验证。基于当前自有 GrassLayerEditor 增量保留生成、重建、保存与关闭按钮；新增尺寸选择和“烘焙当前层基础色”，公开静态 `GrassLayerEditor.BakeTerrainColor(GrassLayer,int=256)` 供 Native 定向调用。

只允许 Edit、已保存的自有 GrassLab 场、独立 Profile/data、单一有效 surface。`terrainSources` 必须显式登记且同场景，至少包含与该 surface 的 MeshCollider 同物体同 Mesh 的基底。额外显式 overlay 须完整位于该 collider 的世界 Bounds 扩展 0.05 米内，法线和实际三角面均朝上；拒绝草层自身 Renderer、墙体和超域层。不做场景扫描，不把不同 surface/高度层混在一图。Baker 会明确拒绝 MaterialPropertyBlock、透明地表及不支持的材质。

首次按 data 路径派生 `_TerrainColor.asset`，已有同身份 map 复用；重复烘焙复用 Texture2D 原路径/GUID，用 `EditorUtility.CopySerialized` 更新原 Native Texture2D 后销毁临时结果。版本递增，surfaceId、worldXZ、线性未受光基础色/alpha有效区语义写入原 SO。纹理只允许自有 `.asset`；拒绝草原图和任何输入地表纹理。配置颜色/风/密度不被烘焙按钮改写。

仅 `SaveAssetIfDirty(texture/map)` 和精确自有场 `SaveScene`，不 `SaveAssets`、不保存 Main/其他场。显示块通过已有 `RebuildFromSaved` 更新。发生保存失败会明确报错，不伪造成功，也不删除他人资产。后续正常保存按钮沿用原 Profile/data 保存边界。

建议 Root Native 验证：Edit 首次烘焙→记录 map/texture 路径与 GUID、版本→重复烘焙同路径同 GUID、版本+1→保存重开比对；单独检查 Play/Main/外部 map/源图/跨层/MPB 拒绝与未改原 Profile/源材质。离线编译只能核 API，不代替这些 Unity 资产生命周期合同。
