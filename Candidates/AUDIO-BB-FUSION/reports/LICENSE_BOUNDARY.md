# AUDIO-BB-FUSION：本地参考内容与公开交付边界

日期：2026-10-27。本文是本任务的工程发布规则，**不构成素材授权证明**。提取成功、哈希一致、技术可用、研究价值或本地拥有原作，都不能被工程记录写成已取得公开再分发授权。

## 只读来源

| 路径 | 用途 | 操作约束 |
|---|---|---|
| `E:/BaiduNetdiskDownload/1/blood/GAME/CUSA03023/dvdroot_ps4` | 原作参考 | 只读，不修补、覆盖、批量重解包 |
| `E:/WORLDCREATOR/XingHaiHuiLang/音声/01_原作参考_不可覆盖/Bloodborne_CUSA03023` | 已提取源、身份和来源记录 | 只读，定向复制选中内容 |
| `E:/WORLDCREATOR/XingHaiHuiLang/实验工程/BloodborneAudioLab` | Lab 源码、已导入素材、实验结果 | 本轮仅参考，不改工程、素材、构建或旧 Trace |
| `E:/WORLDCREATOR/XingHaiHuiLang/参考游戏研究/血源诅咒/08_情境驱动AudioLab_V2/MASTER_FINAL/REFOCUS` | 已封存交付 | 只读；本轮结果另写自己的任务目录 |

Lab 研究身份、匿名 FSB stream 名称和原作事件链的证据等级必须保留。尚未证明完整 FEV 对象引用、原作随机权重、空间混音和地图音乐触发；本轮融合不能改写成“原作 FMOD 已还原”。

## 本机允许保存，公共发布不包含

本地定向素材及其作者资产放在：

`E:/WORLDCREATOR/new/Project_ANKALUTE/Assets/OSSASTRA/Audio/BloodborneLocal/`

以下均属于受限内容闭包，不能只排除 WAV 而留下直接依赖这些 WAV 的公开资源：

- 原始 PCM/压缩音频、提取片段、多通道转立体声、剪裁/归一化/淡边派生。
- 引用上述 AudioClip 的本地 SO、Catalog、BGM Track、完整 Content Profile、Prefab 或 Scene，以及相关 `.meta`。
- 含受限声音的 Unity DSP 录音、A-B 混音、离线预览、播放器构建、AssetBundle 和压缩包。
- 没有清楚再分发许可的旧精选内容，例如 CuratedLocal 和 AudioReferenceLocal，不能因被本轮称为 A0 就默认可公开。

旧本地参考目录同样保持私有：

- `E:/WORLDCREATOR/new/Project_ANKALUTE/Assets/OSSASTRA/Audio/CuratedLocal/`
- `E:/WORLDCREATOR/new/Project_ANKALUTE/Assets/OSSASTRA/Art/AudioReferenceLocal/`

## 可以公开的工程材料

| 材料 | 公开条件 |
|---|---|
| 通用 C# 代码与测试 recipe | 无受限音频字节，不整包复制 Lab；实现唯一根内的通用能力 |
| 语义映射、导入配方和复现步骤 | 只记录稳定语义、FSB/stream 标识、路径、哈希、参数，不内嵌 PCM/base64 |
| 来源/派生 SHA、时长、身份置信度 | 清楚区分来源身份与用途推断，不能把置信度升级为授权 |
| 声音覆盖表、缺口和候选试听队列 | 没有媒体附件；缺口显式，用户听感待签收 |
| 公共诊断媒体 | 仅已有明确自制诊断内容，保持诊断身份，不冒充 Bloodborne 效果 |
| 公共测试场/配置 | 不直接引用本地受限资源；通过本地加载/显式缺失状态进入受控路径 |

## 忽略规则与发布关口

只读审计时，根 `.gitignore` 存在广泛白名单/忽略规则，尚不足以据此宣布本轮私有闭包已验证。**root 在最终交付核验：**

1. 为 BloodborneLocal 与既有本地参考目录保留明确排除，包含目录内所有媒体、SO/native 资产和 `.meta`；将来扩大公共 Audio 白名单也不能误解除排除。
2. 使用实际 Git 忽略/索引核查证明：私有文件未被跟踪、未进入待提交树；`-f` 或历史已跟踪文件不能被“有 .gitignore”掩盖。
3. 扫描本轮公共 Scene/Prefab/SO 的依赖闭包，不留下私有 AudioClip GUID 或直接媒体引用；本地作者配置留在私有目录。
4. 公开 build 不能经 `Resources` 或依赖收集顺带装入本机私有内容；本地参考 build/录音只能留在外部私有工作目录。
5. 保护源库和既有用户 dirty；只发布本轮审定的代码、配方、报告和公共资源，按用户授权仅 GitHub，不 Gitee。

最终索引/构建与保护结果由 root 写入实际交付记录。本文件没有把“规则已写”当作“发布审计已通过”。

## 无内容时的行为和验收措辞

- 有效本地参考档案才显示 Bloodborne 参考模式；缺失内容使用 `REFERENCE_CONTENT_UNAVAILABLE` / `MISSING_SEMANTIC`。
- 不把旧版媒体或诊断提示音悄悄当作参考内容补齐。A0 是显式选择的原内容对照/回退。
- 离线音频必须标 `OFFLINE_PREVIEW`；实际 Unity DSP 采集单独标识；本地含受限音频的录音仍不公开。
- 技术检查和来源哈希只证明其对应的工程事实；听感评价与最终用户验收为 `PENDING_HUMAN`。
