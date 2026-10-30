# 地表工坊镜头俯角微调

- 任务 ID：`kt-ground-camera-angle`
- 状态：`complete`
- 写入模式：`read-only`（全部writer/路径租约释放）
- 开始日期：`2026-10-30`
- 独占路径：无。此前仅Lab相机配置、可选试玩pitch继承、候选与同步；不保存其他用户场景。
- 目标：按用户图2降低当前42.857度俯角，以35度为初始视觉候选，保持旋转/缩放/朝向/脚点链，并持久化作者场。
- 读取：AGENTS/Skill/Sync/共享状态与登记（旧历史此前已完整读，当前只有本对话KT02交接更新）；相机Root与试玩复制/返回路径；原37dirty保持。
- 计划：确认Native上下文→同机位前后渲染→现有pitch配置→读回/保存/关闭重开或试玩返回→发布自有可发布部分。
- 范围：不改角色/地形/镜头控制算法，不新增功能测试。实体操作无需占用桌面。
- 窄接线补充：`Assets/OSSASTRA/Scripts/GroundResearch/Editor/GroundWorkshopPlaygroundSession.cs`只把作者场唯一MapResearchRoot.pitch快照到临时玩法副本；无Root的纯作者图保留模板，无旧模板修改。子代理仅此代码，root唯一Unity操作。
- 验证与交接：Native编译/Edit/Play ready；直接运行pitch与Camera.x均35；从当前地图实际Begin进入临时Scene，pitch=35/Camera.x=34.999996、distance25/FOV35/yaw45；同帧前后NativeCamera.Render根位置不动。返回原Lab clean/35，HasSession=false，保存SHA与进入时相同，Console无error。初次入口因域重载后的派生dirty拒绝；按窗口实际流程先Maps.Save再Begin通过，不清未知场。

## 交付

参数与用法见`docs/ossastra/KT-GROUND-camera-angle.md`；Native回执/参考截图仅本机Candidates/KT-GROUND-CAMERA。场景保存含43个花草显示子节点的派生yaw更新及URP原生补充的相机Data；作者地表格、角色/装饰根位置未改。参考媒体/场景不上传，R11只定向提交自有11行接线、候选源码及说明。原37dirty保持，旧Frontier模板等18保护SHA终核18/18不变；全部租约已释放，无电源动作。
