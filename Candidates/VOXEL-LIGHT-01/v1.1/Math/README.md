# VOXEL-LIGHT-01 v1.1 真实GPU合同

入口：`VoxelFaceGpuContract.cs.txt`，Root通过现有Unity MCP执行IFunplayCommand（skip_refresh）。

- 实际DX11线性ARGBFloat/2×2渲染，不以Shader编译失败、洋红色或静态文本检查作为RED。
- 引入当前生产共享`VoxelSurfaceLighting.hlsl`。没有v1.1宏时，新增控制按原C恒等响应运行；旧80项合同保留，新增项应因控制尚不存在而失败。
- 检查C0恒等、实际主光漫反射+10%/+20%、单调性、阴影/距离/云/无光零贡献、直接镜面不变、暗部最多减半、独立控制、室内/无主光/局部灯保护、连续弱光门控、NaN/Inf中性回退。
- 混合贡献测试直接调用生产`VoxelApplyBackFillControls`，检验GI/登记补光削弱后，局部/未知方向漫反射以及镜面仍保留原值。
- 临时Shader、材质、RT、CPU纹理与CommandBuffer精确释放；不改灯对象、场景、原材质或作者Profile。

本合同测共享数学；填充灯的Unity引用→当前URP剔除灯→全局灯索引的身份解析、Light Layer、实际房间矩阵、普通/Sight渲染、保存重开和Player由Root在当前主场另行定向验证。此处不冒签整场GPU耗时。

候选阈值（不是新增用户规则）：亮面权重smoothstep(0,1,N·L)；背光权重1-smoothstep(-0.2,0.45,N·L)；有效主光从线性强度0.02至0.2平滑启用；局部漫反射/目标填充比值从0.25至1平滑保护。最终控件0–50%；全部是在原C贡献上运算，没有绝对RGB亮度下限。

`VoxelVertexGpuContract.cs.txt` 是逐顶点额外灯变体的独立真实GPU合同：实际定义`_ADDITIONAL_LIGHTS_VERTEX`并直接调用生产背光helper，最大压暗仍必须返回keep=1。聚合额外灯不能证明局部照明的份额，故本变体关闭整项背光压暗而保留原C及主光亮面控制；不尝试猜测或拆分聚合结果。
