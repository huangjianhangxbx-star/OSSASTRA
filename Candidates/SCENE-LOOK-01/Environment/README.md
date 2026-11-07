# 场景环境漫射色度合同

运行：`dotnet run --project Candidates/SCENE-LOOK-01/Environment/ResolverTests.csproj`。

本轮先运行缺少 resolver 的真实 RED（进程 1，feature missing），再添加生产实现；生产数值合同 12 项通过。测试调用生产 `ResolveTint` / `Sanitize`，不是复制解析算法。它检查自然正午/夜晚回归环境色、0° 暖色及两侧连续性、手动天空优先和一次线性解码、无效输入回退、作者配置不被采样改写。

GPU 合同由本轮独立 `EnvironmentGpuContract.cs.txt` 验证，数值合同不替代 GPU 或场景验收。

生产数据流：现有 `EnvironmentSkyBinding` → `SceneEnvironmentColorResolver` → `_EnvironmentSceneTint` / `_EnvironmentSceneChroma` → 现有共享受光 HLSL。没有新根、Update、天气机或观察权限。

- 自然夕照目标使用固定昼夜档 `sunsetSunColor`，不使用地平线下已经切换月色的 `Frame.SunColor`。高度窗口连续，仍由原环境帧决定实际光能。
- 只处理角色之外的场景角色 0/1，且实际表面世界坐标未归属室内。主控所在区域不决定全场色度。
- Voxel 只变更 `gi.diffuse`；Native 许可 PBR 只对本机 URP 的 `bakedGI * BRDF.diffuse * coatTransmission * AO` 添加色度差，保留反射查询；SimpleLit 对乘过 albedo 的间接漫射分量处理。
- Legacy / Sprite / Tile 将原环境贡献单独取出，再把其色度差加回原结果，不染直射、局部灯、minimum 补足、角色、镜面或自发光。
- 关闭、强度 0、无效目标、室内或角色均返回原贡献；黑色不会凭空发光。色度目标先保亮度，再限制极端饱和色归一化的通道增益不超过 4。
- 普通和 Sight 已经共享该 HLSL 的生产入口使用同一规则。原生普通 URP/Lit 没有现有环境适配的对象不会仅因全局变量而改变；须由 Root 的实际覆盖表和显式接收方核对，不能把许可适配的通过声称为所有原生材质都已接入。

已有高亮软肩在累加后继续生效。色度变化可能改变原有峰值保护的压缩倍率；没有新增高亮压缩、亮度下限或最终橙色滤镜。
