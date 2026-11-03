# 环境渲染接入合同与实施边界

- 消费当前相机、本帧正向合法世界输出；拒绝旧CameraDepthTexture。法律许可RGB保留，alpha为明确的环境角色。
- 先验证无Shader时合同RED，再实现新Feature/Shader。GPU合同验证合法环境发生色调变化、角色与未知/混合边界不受影响，以及模糊/Bloom不能从被保护角色或黑幕采样。
- 新Feature仅在已登记Game相机、已启用设置、同帧合法输出同时满足时执行；颜色副本与R32深度共2RT、自有2材质，禁用、撤销与Dispose精确释放。
- 深度从本相机真实受限深度附件读取，自有MSAA1/2/4/8解析；不切全局管线、不打开相机内建后处理。
- 色调/空气/小幅Bloom/景深及暗角仅作用于mask alpha与RG均接近1的环境中心；采样邻域也必须满足相同条件。原2D角色、信息和未知区域原样保留。
- Feature执行顺序AfterRenderingTransparents+1，正向世界之后、Overlay HUD之前；真值模式与失效帧不产生视觉写入。
