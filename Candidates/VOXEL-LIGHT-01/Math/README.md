# VOXEL-LIGHT-01 共享受光 GPU 合同

Root 在 Unity 的 `execute_code` 中执行 `VoxelGpuContract.cs` 全文；此目录不是 Unity 编译目录。

- 首次生产文件不存在时，合同临时 Shader 实际调用当前 URP，预期数值 RED：GI/镜面独立倍率、方向曲线、软肩尚不存在。任何编译错误、未执行片元、洋红色都属于无效实验，不计 RED。
- Root 回传有效 RED 后才创建共享生产 HLSL。同一合同自动 include 生产文件后复跑。
- 中性 BRDF/clear-coat/metallic/spec-off 与原 URP 对照；独立漫反射及镜面倍率、一次 AO、方向曲线端点/单调/连续衰减/视角无关漫反射、同倍率亮部软肩。
- 浮点离屏绘制不经过 WorldLook、曝光或色调映射。测试材质、Shader、RT、command、CPU readback 全部 finally 释放；原 async compilation 与 active RT 恢复。
- 本合同证明共享数学；实际 Forward/clustered、Mixed、Light Layers、云影、许可/普通路由由 Root 做最小真实集成验证，不以本合同代签。

生产 helper 不声明材质参数。普通与许可 Shader 的 UnityPerMaterial CBUFFER 提供任务记录中的 `_Voxel*` 字段。
