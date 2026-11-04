# 角色光感UI接线与Native验收设计

状态：候选；未写生产UI、未运行Native合同。计划包v1.0为需求，Root提供正式服务接口后再接线。

## 窄职责

独立 CharacterLightingPanel，显式绑定CharacterLighting服务、EnvironmentRoot、原CharacterControlCoordinator、MapResearchRoot与已用中文字体。面板位于现环境面板旁，不改原Environment面板、业务HUD或全局EventSystem，不创建新设备InputAction。

结构：自有ScreenSpaceOverlay Canvas（沿用1600×900缩放与当前字体），Header折叠，Body/ScrollRect→Viewport(Image+Mask)→Content(VLG+ContentSizeFitter)，右侧常显Scrollbar。按需单一tooltip浮层，raycastTarget=false，工具提示读取实际控件说明。

观察对象：显式Receiver列表/稳定ActorId。选择仅改变被调节对象，绝不调用协调器Select/SwitchNext或更改Current。不可用对象显示原因并禁用控件，不取第一个全场角色补位。

输入：沿用协调器SetInputCapture(this,value)、map.SetUiCameraCapture(this,value)。本面板hover、pressed、selected或展开dropdown持一份owner租约；失焦、折叠内容销毁/停用、引用变化均精确撤销本owner，保留外部owner。不暂停世界、不硬读键鼠/手柄，Q/E保持原技能。

## 控件与公开入口

- Header：角色光感（展开/收起）。
- ObservedCharacter：观察/调节对象；稳定id及角色名，不切真正主控。
- 三独立Toggle：EnvironmentTintEnabled、ContourEnabled、HighlightProtectionEnabled。
- 三预设Button：OldEffect、EnvironmentOnly、RestrainedContour；使用进入预览时的真实旧值，不从记忆重构夜间配置。
- 染色强度、同步手动天空；轮廓内带宽/柔度/方向性/贡献上限；高亮拐点K/上限C。实际字段由服务签名定。
- BloomEnabled：不新建后端。当前相机post关闭或没有受控Bloom时disabled，旁注明“当前未启用可用Bloom后端；基础效果不依赖Bloom”。
- FreezeClock：只停止EnvironmentRoot.clockRunning，不改Time.timeScale/天气/AI。记录进入状态与本次写值；恢复仅归还本次拥有的时钟变化，不覆盖其它作者后改值。EnvironmentTimeMode.External由外部时刻源推进，禁用冻结控件并显示原因，不能仅设置clockRunning=false就宣称冻结。
- RestorePreview：恢复本轮面板进入前的运行时光感副本及本owner环境时钟变化；不写共享SO。
- SaveCandidateProfile：显式Editor作者操作，保存新候选而非覆盖原共享SO；Player不可用并说明。根任务提供实际保存接口。
- 诊断Text：对象/Profile、源材质→适配材质、alpha输入、room/主要灯/染色来源、缓存尺寸、保护前后亮度。

面板参数刷新使用SetValueWithoutNotify/SetIsOnWithoutNotify，避免刷新触发写入。引用失效时保守停用本面板，不重建角色/服务。自有Canvas/tooltip/可选动态字体对称销毁。

## 等待Root确认的服务合同

1. service与receiver/profile的实际类型、命名空间和显式receiver枚举。
2. runtime Settings快照/写入入口；捕获/恢复及预设如何保留旧参数。
3. 授权环境染色同步开关的唯一归属（EnvironmentSkyBinding或service），避免UI同时写两份。
4. 每Receiver真实诊断字段，观察目标失效时的标识。
5. Editor候选Profile保存方法、路径和失败返回；不得仅改UI文本声称保存。
6. 时钟冻结/恢复如何与环境面板并存；若没有owner时钟API，本面板仅有条件归还自己改过的clock/timeMode。

## NativeUI RED→GREEN

首次执行NativeUiContract.cs.txt：旧生产无CharacterLightingPanel，应返回明确RED=缺少角色光感入口；不能用C#编译错误/全黑图代替。

接入后同合同核对：唯一真实面板、固定scope、可见滚动条/正确ScrollRect层级、关键中文控件、Bloom不可用原因、输入owner获取/归还/外部owner保留、观察选择不变Current、Time.timeScale不变。参数实际写入和恢复/候选保存另按已确认服务接口补合同，不能把toggle视觉状态当领域成功。

真实操作仍由Root：实际主场隔离档Play，用MCP点击/滚动/拖动与屏幕可见性检查。至少确认底部保存入口可滚动到、tooltip不截输入、Slider不攻击/拾取/转镜头，折叠/禁用后租约释放。Native合同只自动断言已覆盖部分。

