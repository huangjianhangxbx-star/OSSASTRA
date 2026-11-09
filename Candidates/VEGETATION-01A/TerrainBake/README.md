# 显式地表基础色烘焙候选

只写此候选目录；未操作Unity、生产、Assets或Git。功能用于v1.2地形取色，不是每帧RTT相机或地形新后端。

## API / 所有权

```csharp
TerrainBaseColorBaker.Result Bake(
 string surfaceId, MeshRenderer[] sources, Bounds worldBounds,
 int resolution, Shader bakeShader, int version = 1);
```

命名空间 `Ossastra.WorldRendering.Vegetation.Editor`，Editor-only静态工具。返回texture、surfaceId、version、sourceCount、worldXZ、worldToUv与未受光/线性声明。`Result.Dispose`销毁其仍持有的Texture；Root需保存时先`var texture=result.DetachTexture()`，再Native `AssetDatabase.CreateAsset(texture, ownPath)`，由保存方接管。工具不创建SO、场景、相机或文件，不调用SaveAssets。

每个surfaceId/高度层分别烘焙。输入是明确的本层MeshRenderer集合，不采世界和其它层；private depth选出本次源集合内顶视可见表面，但不能把本应分层的高台/低地混在一张图中再猜颜色。Bounds必须覆盖当前这一层的实际世界范围，允许非原点、非正方与已旋转Mesh。

`worldXZ = (minX,minZ,sizeX,sizeZ)`。目标合同：`uv = (world.xz-worldXZ.xy)/worldXZ.zw`，u朝+X、v朝+Z；worldToUv矩阵的xy与此相同，不受世界Y影响。alpha1表示明确表面覆盖，alpha0是无表面或源alpha裁剪区域；bounds内部不等于全有效。运行Shader须先检查UV在0到1范围，再检查采样alpha；Clamp寻址本身不是有效范围判断。

## 基础色与受限支持

只处理常规mesh UV的URP Lit/SimpleLit/Unlit、Unlit Texture/Color、Standard基础图/tint。优先_BaseMap/_BaseColor，兼容_MainTex/_Color，保留texture ST、已有顶点色、AlphaClip/Cutoff。拒绝透明混合地表、未知自定义程序世界UV/复合材质，避免把错误基础色冒称精确原地表合成。源 `renderer.HasPropertyBlock()` 在分配私有GPU资源前明确拒绝，不静默忽略；额外detail层不在首版支持范围。当前Lab地面应由Root核对使用独立普通基础材质。

烘焙Shader是专用unlit输出，不包含光照、天空、主光阴影、后处理或草。没有传入的Renderer绝不会被绘制。只用显式DrawMeshNow和自己的材质矩阵，无Camera.Render/RenderWithShader，也不写Shader globals或GL矩阵。RT.active与临时GL.sRGBWrite finally归还，临时材质/RT销毁；故障时输出纹理也销毁。

输出Texture2D为RGBA32 **linear=true**，RT也使用Linear；后续Shader只采样一次。若导出PNG后再导入，必须另行正确处理linear/sRGB，不能当默认sRGB图片重复解码。Root优先直接Native保存此Texture2D资产，保留语义。

## Root导入及必要GPU验证

- `TerrainBaseColorBaker.cs.txt` → `Scripts/WorldRendering/Vegetation/Editor/TerrainBaseColorBaker.cs`。
- `TerrainBaseColorBake.shader` → `Scripts/WorldRendering/Vegetation/Rendering/TerrainBaseColorBake.shader`。
- 然后Root在Edit执行`ValidateTerrainBake.cs.txt`，**必须先验证而非只看编译**。

三色合同：世界XZ为[-4,2]×[10,15]（偏移非正方），左半红，右下绿，右上蓝。蓝色表面高0.3且先绘，绿色后绘，证明不是仅靠顺序覆盖。GetPixel(16,32)/(48,16)/(48,48)应分别红/绿/蓝，验证v朝+Z及深度。第二张去掉绿色源，右下alpha应0，证明有效区。还核RT.active精确归还、源材质CRC不变、center世界Y任意时uv=.5/.5。

该合同由Root实际GPU运行后才可记录通过。顶视矩阵使用GL.GetGPUProjectionMatrix(Ortho,true)；若当前团结/D3D方向或depth行为不匹配，合同应失败并局部修正，不可悄悄把世界UV文档反过来掩盖。子任务未调用Unity，因此没有GPU通过结论。
