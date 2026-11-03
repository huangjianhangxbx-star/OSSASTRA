# 既有3D渲染接入主场

日期：2026-11-03。用户已授权连续实现，延续当前认证主场，不另建替代主场。

目标：把已选择的T7C3 Light环境表现接入现有3D URP主场，保留45°、双角色单敌人、房间/窗洞可视权与原业务，并删除已无引用的旧验证副本。

## 已确认的设计

当前主场本来使用XH_URP3D的UniversalRenderer。旧CompositionController是会替换全局管线的实验预览器，不能直接挂入主场。接入使用相机专属RendererFeature，在合法世界重绘之后处理环境；原Unity后处理保持关闭，不能使用重绘前的旧世界深度。

RoomPositiveSurface增加独立环境角色，环境角色与黑幕静态历史是不同概念。场景Mesh/装饰Sprite可参与；角色、敌人、Spine、粒子和登记信息不参与。标签许可RGB合同保持，Alpha提供环境掩码，人物最后排除。正向绘制成功后发布当相机/当帧的许可与合法深度；失败、撤销、销毁时撤销。Feature自行解析MSAA深度，环境效果中心及采样邻点都需通过许可，不把未知黑幕或角色颜色采入。

新Controller显式引用旧OutdoorAtmosphere与HD2DPostProfile，只读观察点和室内外身份发布快照。室外Light与室内Dungeon Light，Inspector可关闭全部/空气/景深/泛光/调色对照。原局部灯、屋顶遮光、材质、碰撞、存档和Current权威保持。旧Sunset原生Bokeh、Dungeon体积光和RealToon资产专用色板不冒充为此次通用后处理已完整复制。

## 实施与验收

- [x] Native确认主场、3D管线、相机与构建表；保存用户当前dirty主场的独立安全副本到Assets外。
- [x] 三项RED：环境角色未定义、Look快照未实现、环境GPUShader缺失。先运行真实契约，再写最小实现。
- [ ] 环境角色与Alpha输出、成功帧桥；新Runtime Controller与RendererFeature并行互斥实现。
- [ ] Native编译；运行参数/角色契约与实际GPU颜色、保护、黑幕、采样边界契约。
- [ ] 给当前主场相机专用Renderer增加Feature，挂显式Controller引用；同场开启/关闭对照、屋内外/窗外检查；资源撤销重入、保存重开确认。
- [x] 精确17场Native GUID往返/6633序列化资产反向依赖/当前构建表/已打开场景核验通过。
- [ ] GUID文本检索；逐文件SHA外部备份后，Native精确删除17场；不删共享素材、不夹带原31个删除。
- [ ] 复核保护基线、定向GitHub自有源码测试文档，记录真实结果与限制，释放租约。

## 文件边界

- WorldRendering/Runtime：只读Profile到相机快照，不改变全局管线。
- WorldRendering/Rendering：合法帧桥、MSAA深度解析、环境合成与相机资源生命周期。
- SceneVisibility/Rooms：环境角色与标签Alpha/成功合法帧最小缝；不改变视野算法。
- Candidates/RENDER-INTEGRATE-01：Native行为契约、清理清单和取证脚本。
- 本机主场及自有Renderer配置：只经Unity API保存；代码发布不携带媒体或整场资产。
