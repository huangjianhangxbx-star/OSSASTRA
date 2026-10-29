# DS-MAP-01｜最终实现与角色接线

本文件依据本机最终源码与本轮Unity实测记录，区分工程适配、原作证据与未验边界。研究场入口为`Assets/OSSASTRA/Scenes/Research/SC_DontStarveMapLab.unity`；不修改正式启动场景、全局管线或Q/E技能绑定。

## 场景与程序集

- `Scripts/MapResearch/Runtime`为独立`Ossastra.MapResearch.Runtime`程序集，只引用Input System与`Ossastra.Characters.ViewSpace`。
- `Scripts/Characters/ViewSpace`为仅依赖UnityEngine的通用程序集；契约、投影数学、适配器均不引用MapResearch、Spine、原作素材或Assembly-CSharp。
- 现有CharacterRoot和其他角色实现仍在原程序集，通过自动引用消费ViewSpace；没有为隔离文件夹而搬迁整套角色。
- Editor装配器从已保存`SC_CommerceIntegration`读取Blue角色实例的配置，继承既有Blue五面Prefab，生成`MapResearch/Prefabs/PlayerCharacter_MapView.prefab`。
- 装配保留原本体控制、仲裁、动作、资源、装备及动画链；去掉场景专用伙伴脑、伙伴库存消费者、开发控制端和旧子相机，并重映射内部引用。
- 研究角色的相机引用在保存Prefab后仅覆写本场实例；基础Prefab、原场景及原模型不被保存回写。原作人物没有导入。

依赖方向：`MapResearch场景 → ICharacterViewParticipant / ICharacterViewContext ← CharacterRoot → 可选ViewSpace`。Characters没有反向引用实验业务。

## Root管理的可选能力

`CharacterRoot`实现`ICharacterViewParticipant`，通过明确owner绑定上下文、捕获输入、释放租约；不在绑定时自动添加模块。

`CharacterViewSpaceAdapter`必须配置纯视觉子层，本场指定`VisualFlipRoot`。初始化拒绝角色根或角色层级外的Transform；模块没有Update、LateUpdate或设备读取。

正常角色每帧顺序为：

1. `MapResearchRoot.Update`以执行顺序-300更新实际相机并发布观察基。
2. `CharacterRoot.Update`以-100先调用适配器Tick，缓存本帧实际水平基及视觉平面。
3. 原`StateArbitrator.ManagedUpdate`读取唯一有效输入，推进资源、方向、动作受理、动作时钟、效果和真实运动。
4. 原Orientation完成世界显示方向；适配器只计算观察镜像，Root提交原表现端口。
5. Root.LateUpdate沿原流程FlushSignals并更新装备、临时道具挂点。

Root禁用时Suspend恢复原视觉旋转；重新推进时恢复有效上下文。销毁或场景精确释放owner时移除自己的输入捕获、恢复原相机引用并Dispose模块。没有模块、模块关闭或上下文无效时，输入和方向沿原路径。

## 玩家意图只转换一次

唯一转换点在`CharacterIntentSelector.Read`的Player分支：`player.Read() → Root.MapPlayerFrame`。选择器发布的有效帧仍由原DataBus读取，不增加第二份可写Frame。

- 玩家move用缓存的实际相机水平right/forward映射到世界XZ，保留幅值；原Movement仍执行唯一CharacterController.Move与碰撞。
- 实际手柄移动时转换新aim意图；空闲手柄读取Orientation已持有的世界LastMoveDirection，不把玩家会话中的屏幕方向记忆反复旋转。
- 无移动历史或新生命沿原Orientation出生/Reset规则；没有新增方向记忆权威。
- 鼠标pointer保留屏幕坐标，由Root经明确的GameplayCamera求世界平面点。键鼠无指针时的fallback已来自Bus世界攻击方向，不再转换。
- Companion分支提供的跟随/AI世界向量原样传递，不走玩家相机基映射；原来源选择、设备归属与权限规则保留。

空闲镜头旋转曾使手柄世界历史改变，真实Root→Selector→Orientation红测为9通过/4失败；窄修后13/13通过。

## 世界方向、动画与装备

Bus的`AttackDirection`、`PresentationDirection`、LastMoveDirection及原动作状态仍由各自所有者提供。新增`ViewPresentationDirection`和`ViewFacingX`仅为只读观察投影，不反写世界方向。

五面`CharacterDirectionPresentation`消费观察方向，沿既有SO扇区及镜像规则选择结构轨；单面原表现端口消费观察镜像。没有更改原方向SO数值、骨架数据、模型缩放或动作SO。

原`CharacterAnimationController → Config/Router → Composer → AnimationHandleSystem`仍是身体主轨提交链。换面只更新授权的零时长结构轨9；Handle按逐轨身份去重，不因镜头换面重播主轨0、重新受理动作、扣费或结算命中。

装备继续经原骨骼/插槽、方向/片段/阶段路由和原LateUpdate求挂点；身体视觉平面变化由子层继承，不将世界投射物重新挂回视觉层。

鼠标普攻保留原释放当帧采样政策，前摇仍可重新瞄准；本轮没有把所有动作改成接受时锁向。已冻结机动和已释放投递继续持有原世界方向。静止旋转验证的逻辑方向指原世界显示/移动历史，不把鼠标候选世界目标变化误判为冻结规则。

## 相机、固定世界与结构模式

地图、物件世界根、角色根、碰撞与命中空间不挂到旋转WorldRoot。相机围绕观察目标转动；物件显示子层及人物视觉子层分别适配观察方向。

MapRoot首次在Start绑定，等待所有Awake/OnEnable完成；后续重新启用由OnEnable重新绑定。研究场已显式设置Root所需游戏相机，Start不用于补救一个因Awake缺依赖而停用的角色。

每帧发布实际相机旋转及其水平正交基，过渡中不使用尚未到达的目标角。滚轮/手柄十字键左右每次请求45°；方向键仍移动，Q/E仍为技能，PageUp/Down独立缩放。

进入结构模式后，正常相机与已显示平面/观察基冻结，独立观察相机侧看或俯视；观察相机不替代鼠标瞄准相机。适配器缓存平面与方向基一起恢复，已修复无效上下文恢复后冻结平面未重应用的问题。

结构模式通过原`SetInventoryCapture(owner, captured, true)`捕获角色输入，不停用Root，不修改TimeScale。准确边界是：

- 一次性已受理动作继续原时钟与唯一结算。
- 移动捕获会执行原手势重置；持续依赖Held的盾防/蓄力按原UI捕获规则结束，不宣称持续能力被原样保留或暂停。
- 已实测shield-a右键保持Guard=true，F6后Guard=false、busy=false，接受次数仍为1，无重新受理。退出后须先松键再重新按下。
- 失焦退出结构并丢弃镜头待处理输入；地图研究根禁用/销毁时释放自己的owner、Action及指针捕获，不清其他系统的租约。

## 局部深度Shader及限制

`Ossastra/MapResearch/DepthPlane`仅用于样地材质副本和角色Renderer材质替换，不改原图集/共享角色材质或全局Renderer。

该Unlit Pass采用Cull Off、ZWrite On、ZTest LEqual、AlphaTest队列、SrcAlpha混合；按合成alpha阈值裁掉透明轮廓，保留Spine顶点alpha显隐，并按图集来源处理PMA输入。

这是一种Unity混合遮挡适配，不是已证明的饥荒原生实现。超过阈值的半透明像素仍写深度；多层半透明、交叉纸片、柔边、阴影及正式受光不因此自动正确。它没有完整正式Spine材质的所有功能，不能据此宣称透明排序完美或美术最终方案成立。

## 真实验证范围

- 八观察角四向移动：91/0；八角攻击身份、主轨、费用与真实命中：48/48。
- 四次点击连段、闪避及生命周期：31/0；原攻击夹具49项中曾有1项把“按住自动四段”作为预期，这是夹具假设错误，后续按原点击规则验证，不修改玩法来迎合错误假设。
- 观察数学/契约162/162、适配器生命周期37/37、Root世界历史13/13；Edit显式Suspend检查与真实Play生命周期证据分别记录，不互相替代。
- 这些结论覆盖本研究场选定Blue实例与本轮输入/动作，不代表所有正式场景、模型、装备或实体手柄已全面验收。
- 原作启动观察受阻，未完成原作八角动态视觉对照；技术通过不代签用户视觉/架构审查，也不宣称已完成正式地图系统。

操作见[user-guide.md](user-guide.md)，源事实与适配差异见[source-to-unity-mapping.md](source-to-unity-mapping.md)。本机测试收据、截图和录屏保留在外部研究根；受限媒体、原作代码与派生素材不公开分发。
