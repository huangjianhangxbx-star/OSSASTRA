# 角色光感面板代码交接

## 已实现

仅写独占 `Assets/OSSASTRA/Scripts/Development/CharacterLighting/` 四个新脚本；原环境面板、协调器、地图与其他生产源码未修改。

- `CharacterLightingPanel.cs`：独立折叠窗口、右侧常显滚动条、按角色观察与运行副本调参、三独立开关/预设、条件恢复、真实诊断入口。
- `CharacterLightingPanelWidgets.cs`：沿用环境面板配色/1600×900缩放的自有uGUI叶；不查全场、不建EventSystem。
- `CharacterLightingPanelInputRelay.cs`：沿原协调器/map owner租约捕获指针/拖动/导航，带中文tooltip，禁用/销毁精确释放。
- `CharacterLightingProfileSave.cs`：仅显式Editor操作，生成唯一新Profile资产，Clone+Sanitize，不覆盖原SO、不自动改Receiver.profile、不全局SaveAssets。

新窗口默认锚右上，offset(-404,-63)、width350/height620，初始折叠。Root应实际截图核对；不是已通过画面验收的声明。

## Root接线

Panel明确字段：`receivers`、`environment`(EnvironmentRoot)、`coordinator`、`map`、`environmentPanel`、`font`。

`public Func<CharacterLightingReceiver,string> DiagnosticsReader` 绑定 `system.Describe`，服务OnEnable/组装后重接，不从UI猜房间/主要灯/GPU数值。源材质/alpha/覆盖等细节由Root服务提供。

所有runtime编辑只写`receiver.Settings`，Restore经setter Clone，Profile仅首次运行复制。总开关`system.effectsEnabled`仍归Inspector，UI不复制第二份。

时钟仅PreviewClock/Manual可冻结，External禁用；归还时只在clock/timeMode仍等于本窗口写值时恢复原值。不改时刻小时、天气、TimeScale或AI。

## 验证事实

- Root已回报旧生产运行 `NativeUiContract.cs.txt` 得到 fixtureReady=true 的“缺少角色光感入口”真实RED，无测试错误。
- 四脚本连同实际Core/Receiver/Cache与两份Native合同，在`PanelCompile.csproj`离线C#编译0错误0警告。
- 编译中定位Receiver未限定CharacterRoot误绑定旧全局类型，已通知coverage修正；随后真实依赖重编译通过。测试project曾重复UnityEditor引用，已仅在候选引用配置去重。
- 未由本子代理运行Unity/Native GREEN或截图/保存检查；唯一Unity writer仍是Root。C#编译不代表UI或GPU已通过。

## Root待跑的小集

1. `NativeUiContract.cs.txt`：真实唯一窗口、ScrollRect/常显bar、11控件、输入owner获取释放/外国owner保留、实际观察Dropdown选择不切Current、停用释放、不暂停。
2. `PanelValueContract.cs.txt`：真实Toggle/Slider→Settings、Sanitize、另一角色/共享SO不变、恢复、三预设、冻结/解冻/External禁用、Current不变、诊断委托绑定。finally归还副本/时钟/展开/选择；不创建候选资产。
3. 显式保存一个新候选Profile，再Native读回settings/源SO不变/唯一新路径；根任务决定保留为交付候选或精确删除测试资产。
4. 实际MCP点击/滚动/拖动与截图，确认中文、tooltip、位置、底部入口、输入不攻击/拾取/转镜头；关闭/重开与服务OnEnable诊断重接。

以上不是已通过清单。受测Unity工作树非隔离，Root保留源角色、原参数与业务路由，测试仅明确隔离profile。
