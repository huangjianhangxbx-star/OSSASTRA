# 装饰作者服务实现约定

入口集中在 `Editor/GroundWorkshopDecorations.cs`，四个方法保持主代理已冻结签名：Place、Scatter、EraseGenerated、SetProtected。运行态只由每地图一个 `GroundDecorationOrientation` 派生视觉；地表Shader不修改，装饰用独立 Cutout Shader 和地图自有唯一材质。

作者数据为原生实例根Transform、GroundDecorationInstance身份/来源/锁、定义引用。视觉子物件的旋转及pivot补偿是派生数据；不建立位置JSON，不在相机/重绘/Play入口生成实例。Flat保持平铺，FixedUpright继承作者根，UprightYaw仅随相机绕世界Y旋转；不修改相机或根TRS。

所有写入口先调用统一地图所有权预检，再检查真实定义、范围和工具实例结构。范围采用逻辑格XY并裁到地图边界。Scatter采用独立稳定PRNG，种子混合显式seed、区域、主题/变体版本及配置；目标数量来自真实世界面积且上限512，尝试次数有界。道路来自真实Road Tilemap，并计主题的排除格数。保留手摆、锁、外部子物件及区域外物件，将它们作为间距障碍；只替换本区域本工具未锁生成实例。

每个作者操作只有一个Undo组。新对象、删除对象和新材质分别记入本次账本；成功收束本组，失败在组未漂移时只撤销本组。检测到外部组变更后不跨组Revert或Collapse，改用本次对象账本恢复自身作者数据并明确报告，保留外部操作。新材质只保存自己的资产，不全局SaveAssets。

测试从真实Native缺服务RED开始，再在已登记地图验证Place单Undo/Redo、错pivot脚点、多相机yaw、稳定重撒、间距、道路排除、手摆/锁/外部保留与非法入口拒绝；测试只撤销自己的受控区间并删除自己创建的临时定义资产。
