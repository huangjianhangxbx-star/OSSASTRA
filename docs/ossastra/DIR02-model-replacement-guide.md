# DIR02 后续五面模型替换指南

1. 保留当前Root/玩法组件，派生新的候选Prefab Variant；只换身体SkeletonDataAsset、允许的视觉比例/脚点。不要缩放物理根或使用整骨架阵列bounds适配。
2. 按正式Spine4.3运行库导出源工程副本。Linear项目用straight-alpha图集与匹配材质，检查NPOT尺寸、区域名/trim/rotation；不通过改版本字符串或切全项目Gamma解决。
3. 新建原式Animation Mapping、模型Override，映射既有语义枚举。每个正常可达动作必须有合法clip；近似片段明确PRESENTATION_PARTIAL。原装备定义与Gameplay时间线不因换模型更改。
4. 新建Direction Profile：模式MultiFace；明确世界XZ的atan2(x,z)及yawOffset；逐条配置face/root/logicId、扇区顺序/镜像、初始面、结构轨、边界slot、内置武器slot。所有方向片段单关键帧0时长、mix0、不按动作时长缩放、无队列/清轨。片段名仍在原Mapping，不在控制器写死。
5. CharacterDefinition引用新方向Profile和Rig。身体保留原Controller/Config/Composer/Handle，加DirectionPresentation。Config指定新模型覆盖，Root.view必须是这一身体；清除原身体引用，不能隐藏一个旧播放器后再添一个新播放器。
6. Rig按功能语义建立默认绑定，再按面/具名动作时间段配置variants。顺序优先，动作阶段放面通用规则之前；区间左闭右开，终点1包含。已有equipment.right/left/item.use不随镜像交换玩法身份。使用真实可见手骨，接管需有姿态证据；不使用最近骨/名字相似猜测。
7. 骨名、slot、point及阶段是本模型配置。修改配置后增加contentVersion，消费者会重绑缓存。不要回写共享SO的当前方向。装备本身Skeleton/grip/语义动画继续由独立装备SO配置；切面不换装备资源。
8. 两种显示模式分别检查：SourceReference不显示额外装备；IndependentEquipment按具名slot额外mask内置武器并显示独立Spine。整体Renderer排序只能表达整件前后关系，复杂内部穿插需要另行拆Renderer等方案，不能改伤害范围掩盖视觉偏移。
9. 在候选对照场测试静止同X符号换面、四段空挥/实击、接受与释放锁向、闪避/格挡/技能、a4式辅助接管、切控、死亡返场、禁启/重建、两个共享数据实例，以及旧单面角色回归。

作者入口：选角色根看“身体动画与配置”，定位Direction Profile、Rig、Base Map、Router、模型动作覆盖；Play只读区显示实际结果。Workbench选CharacterDefinition可跳转依赖SO，选具名Play角色可显示同帧功能挂点。

小蓝骨名/30条表不是芙雷茜娅必须照抄的标准。用户源工程、骨架约束和图片路径不由本轮自动修改。新机器导入本机媒体后，需把新模型通过Inspector重新指定到候选身体、Mapping/模型Override各clip及Rig.model；在本机既有路径重跑ImportBlue不会重新生成GUID，生成器不会覆盖已存在候选。
