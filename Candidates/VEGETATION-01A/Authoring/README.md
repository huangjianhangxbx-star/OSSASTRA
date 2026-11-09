# 草场作者与观察候选

只写本Authoring候选目录，没有Unity/生产/Git操作。Root导入建议：

- `GrassLayerEditor.cs.txt` → `Assets/OSSASTRA/Scripts/WorldRendering/Vegetation/Editor/GrassLayerEditor.cs`。
- `GrassLabControls.cs.txt` → `Assets/OSSASTRA/Scripts/WorldRendering/Vegetation/Development/GrassLabControls.cs`。

真实依赖已核对：GrassLayer/GrassLayerProfileSO/GrassPatchDataSO/GrassSurfaceBinding(MeshCollider)/GrassPlacement，以及既有MapOrbitState。编译使用当前候选Runtime/Scatter及当前项目程序集；不是Native运行证明。

## 作者操作

自定义Inspector保留DrawDefaultInspector；生成、重建、保存只在Edit显式点击。scene/profile/data都必须在 `Assets/OSSASTRA/WorldRendering/Vegetation/GrassLab/` 下，拒绝Main、未命名场或外部配置。生成成功采样后才把placements、seed、density、maxDensity、surfaceIds写入独立data；显示块通过RebuildFromSaved重建。保存仅SaveAssetIfDirty(profile/data)+SaveScene(此自有草场)，不调用SaveAssets，不写原图/碰撞。关闭仅SetMode(Original)。

## 场内控制

控制器只对GrassLab已保存场路径生效；引用本场layers、camera、EnvironmentRoot、avatar（静态代理脚底根）。`avatarVisual`可选，仅允许avatar后代，用于代理卡片朝镜头；没有赋值就不旋转任何角色支系。代理不带真实角色控制、动作、碰撞或Coordinator验证语义。

公开Native入口：

```csharp
SetMode(GrassWindMode value, bool castShadows);
bool RequestRotation(int direction);
void ResetView();
void RefreshView();
void SetWindDirection(Vector2 direction);
bool SetTimeHours(double hours);
bool MoveAvatar(Vector2 input, float deltaSeconds);
```

MapOrbitState提供八档45°平滑旋转；镜头俯角45/FOV45，默认distance19、initialYaw315。左右箭头和滚轮每次一档；WASD摄像机相对移动代理，只对已登记surface.collider.Raycast，拒绝明显高度跳跃/墙面，不全场Physics.Raycast。`followAvatar`可切；`showPanel=false`便于成本采样，`acceptKeyboardInput=false`便于稳定自动路线。没有Time.timeScale、Advance天气或额外时钟。

五风态、独立草阴影、试验风向X/Z、本场12/18/0时切换均有按钮。统计显示生成量/块/三角/顶点及“当前引用材质”去重数，明确不是GPU批次或全部owned材质数。界面不宣称Room/门窗黑幕集成。

## 检查结果与Root剩余验证

Editor和Player条件编译均0警告/0错误；起初单独额外引用UnityEditor.dll造成CoreModule重复类型，已修正编译夹具，不是产品逻辑失败。Root仍需在Native编辑态核Inspector生成/保存/重开，Play核五态、旋转、代理贴地路线及视觉/成本；子任务未调用Unity。
