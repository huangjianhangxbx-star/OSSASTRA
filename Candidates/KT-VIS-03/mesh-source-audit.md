# KT-VIS-03 原石块网格与材质只读核对

日期2026-11-01。只读原指定研究数据、其已明确索引的原normalmap包，以及当前本地石块Mesh资产索引；不操作Unity/Git，不修改原包、模型、材质、场景、Prefab或生产源码。唯一写入本笔记。

## 已确认的直接结论

当前`KT_Stone3D`所用原`gather_shi_2`是**有体积起伏、但侧面有开口的定向3D壳**，并非各方向闭合的石块。原始索引已经如此，当前导入索引与原JSON完全一致；不能把从缺面一侧看见顶盖，归为导入绕序反转，也不能把`Cull Off`冒称原作材质方案。

原实例子节点本来带Y135°旋转，我方样板当前Body为Y0°。Root应先在同一原材质/Cull2、同一镜头下单独对比Y135°。若需适应本原型可旋转地图，可明确仅让这类定向壳使用既有`WorldPropsRoot.faceCamera`水平朝向适配，并保留实测合适的Body局部偏角；这是我方对定向参考资产的适配，不是声称原作有同样的自由旋转机制。闭合的其他3D模型不据此改成朝相机或双面。

## 原始身份与读取范围

- 原作：Frontier Tale Demo，app5010220/build26043868；本轮未启动原作。
- 精确原包：`D:/steam/steamapps/common/开拓物语 Demo/FrontierTale_Data/StreamingAssets/aa/StandaloneWindows64/normalmap_assets_all_965f56dfdf98b1ed3efd4f7a371e346f.bundle`。
- 原研究记录的包SHA256：`f215aafe320098f3ccd1dc7f19b79e8550366b14d146c96e4454b538802cc1b6`；本轮未重新全包哈希，不把旧SHA写成新核验。
- 采用原已有`lazy_bundle.py`懒读器，最多缓存4个解压块，仅查既有Map_Kaituo_Starter资产文件中的Material `-1223574832596115917`、Shader `855815294000924952`，未全量导出媒体/Shader/反编译。
- 原局部几何权威文件：`E:/WORLDCREATOR/XingHaiHuiLang/研究/场景物破坏与掉落/WORLD-PROP-01/02-frontier-tale/selected-assets/gather_shi_2.mesh.json`，本轮读得SHA256 `ad3461e1bf729c6ab34f940fcb92f95fb05a9e4017f1b33d65dc379362548e7c`。
- 原模型节点：`/gather_shi_2_-75.53_0.00_-55.18/gather_shi_2`，Mesh ID `-1728424562674400328`，原Material名`gather_shi_2`。

## 原材质与Shader状态

精确原Material的序列化值：

| 属性 | 实际值 |
|---|---|
| Shader | `Custom/URP_ShadowBoost_Switch` |
| `_Cull` | 2（Back） |
| `m_DoubleSidedGI` | false |
| `_Surface` / `_ZWrite` | 0 / 1 |
| `m_CustomRenderQueue` | 2450 |
| `_AlphaClip` / `_Cutoff` / `_AlphaToMask` | 1 / .5 / 1 |
| `_UseDistanceFade` | 0 |
| `_UseTreeWind` / `_UseVegetationWind` | 0 / 0 |
| `_UseHitShake` | 1 |

原Shader的Forward与ShadowCaster实际序列化pass state均把culling绑定`_Cull`；不是仅有一个未使用的同名属性。Forward深度测试为LEqual，zWrite绑定`_ZWrite`；ShadowCaster zWrite为常量1。以上证明读到的原资产/编译状态，不代替游戏运行时材质property-block或动态参数审计。`_AlphaClip=1`只是本轮已读的原浮点值，不声称审查了其全部编译片段逻辑。

原albedo PNG为85×85 RGBA，Alpha范围0–255；本轮文件SHA256 `511df3d64c1018f8fc020a20c0fe7592826ffa61a7599670f5f2f791539e2922`。不能由“像顶盖”就推断原图被裁一半。

## 原变换、拓扑和当前导入核对

- 原实例根：本地缩放(1,1,1)，旋转identity。
- 原Mesh子节点：本地位置(0,0,0)、缩放(1,1,1)、四元数(0,.92387956,0,.38268343)，即Y135°；没有负轴缩放。
- 原Mesh为30顶点、32三角、96索引，索引范围0–29，局部Y约0.00000067至1.61830497。旧记录signedVolume +1.9035556659703414；开口网格的该值不能用作封闭体积证明。
- 按位置四舍五入至1e-5焊接UV分裂顶点后，24个顶点、55条无向边：41条被两面使用，14条只用一次。这14条边构成单一开口边界，跨越Y0底部与Y1.5上部，**不只是底盖未封**。41条共享边方向相反，没有发现共享边同向的局部绕序冲突。
- 原32个三角的cross与其原顶点normal平均值点积全部为正，负值0；不是某一半三角被意外翻转。
- 读当前`Assets/OSSASTRA/Art/WorldPropsReferenceLocal/KT_Stone3D.asset`：其96个16位`m_IndexBuffer`值与原JSON扁平索引逐个完全一致，并非交换每组三角的后两个点。
- 核对导出工具使用的UnityPy `MeshHandler.get_triangles()`源码：Triangles拓扑直接返回原索引三元组；未做OBJ手性翻转。不能把OBJ显示约定套到JSON重建上。

只用于定位朝向的平行视线估算：35°俯角、45°yaw时，原局部三角法线按Body Y0°投影，8面朝相机/24背离；Body Y135°时24面朝相机/8背离。该估算不是Native画面PASS；确切遮挡、透视投影与形状完整性由Root的实际Mesh差分和转向对照给出。

## 本轮未做与交接

未运行原作、未改Cull/绕序/几何/材质/Prefab/代码，也未运行任何Unity探针。Root正在分别验证真实Mesh源图、加实际地表、诊断Cull2/Cull0与原Y135°朝向。当前事实支持“定向开口壳在错误朝向观察”这一可检验原因；最终修复范围和Native通过结论由Root签署。

笔记及只读研究租约已交回Root。
