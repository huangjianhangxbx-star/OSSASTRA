# DS-MAP-01｜原作证据与Unity适配对照

本文约束研究结果的解释；Unity最终落地和验收以implementation/validation记录为准。这里的SOURCE_VERIFIED不代表原作实机已经观察，UNITY_ADAPTATION也不冒称原生实现。

| 主题 | 原作证据 | Unity等效方案/边界 | 证据等级 |
|---|---|---|---|
| 相机45°档 | playercontroller RotLeft/Right±45，长按.25s重复 | 相机绕现有角色观察目标；不旋转世界根；过渡取最短路径 | 源参数SOURCE_VERIFIED；Unity插值/档位实现单独验证 |
| 输入 | 原生旋转控制与角色控制可同时存在 | 用户改用滚轮/可选十字键箭头，Q/E保持原技能；缩放独立Action | 用户明确修订＋UNITY_ADAPTATION |
| 观察角 | heading45、distance30、offsetY1.5、FOV35、pitch默认42.857 | 可校准机位；不为贴合原图缩放现有角色碰撞 | SOURCE_VERIFIED参数＋UNITY_ADAPTATION机位 |
| 投影 | Lua只提交FOV，原生矩阵不可见 | 透视FOV35；不宣称矩阵完全相同 | 推断/UNKNOWN，非源码确证 |
| 空间 | 源XZ地面、TILE_SCALE4；实体保存x/z | Unity保持XZ/Y上和现有角色世界尺寸；地表4单位逻辑格 | 源SOURCE_VERIFIED；Unity比例显式配置 |
| 物件放置 | Node可加连续偏移；placer默认世界点 | 世界根连续放置；格线只显示逻辑格，不暗示所有物件锁格 | SOURCE_VERIFIED原则＋UNITY_ADAPTATION手工样地 |
| 随机世界 | WorldSim/Voronoi与原布局文件可见 | 本轮手工小样地，不复刻用户存档/生成算法/流送 | UNITY_ADAPTATION |
| 地表原图 | grass/forest/dirt图集和XML透明边缘，另有noise | 使用真实彩色裁片＋原图透明边界；可乘共享noise，周期需标样地校准 | SOURCE_VERIFIED素材；UNITY_ADAPTATION选片/周期 |
| 地表邻接 | native选边算法未取得 | 按PNG角点alpha选择15个非空mask片，不称native算法还原 | UNITY_ADAPTATION |
| 场景物件朝向 | Default=Billboard且selected prefab无覆盖；facing255 | 依相机显示同一完整物件帧，原世界根/占地不动 | 默认SOURCE_VERIFIED；具体矩阵UNITY_ADAPTATION |
| 图片根与帧 | BILD三角UV、ANIM仿射/层次和root | 共同画布、固定root pivot、sheet rect/原时长；不逐帧重居中 | 数据SOURCE_VERIFIED；离线烘焙UNITY_ADAPTATION |
| 像素与世界比例 | ktools记录图集编译1/3比例；native每米像素未知 | 烘焙1/3，PPU100作为显式校准；角色本身尺度保留 | UNITY_ADAPTATION，不冒认native PPU |
| 碰撞 | 树.25、岩石1、浆果丛.1、火坑.3；草树苗箱无obstacle | 小底座3D碰撞独立于大树冠；若为诊断新增占地须注明 | 源SOURCE_VERIFIED；Unity边界单独配置 |
| 季节与火焰 | snow另行Override且非冬Hide；火坑火焰是额外FX | 只无冬雪/无火火坑；无角色/音频/季节全提取 | 已明确选择范围 |
| 遮挡/影子 | 原生深度/阴影算法未取得 | 当前相机脚点深度或本地材质/深度策略，真实绕行验证 | UNKNOWN原生；UNITY_ADAPTATION实现 |
| 人物 | 仅研究原作人物与世界的空间关系；不提取其媒体 | 使用现有CharacterRoot/仲裁器/动画SO/Spine，不另写Motor | 用户要求；实际Root时序由实现报告核验 |
| 逻辑与表现朝向 | 不能据nativeBillboard推导星骸权威方向 | 相机只影响玩家屏幕意图的一次转换及view-relative表现；AI世界向量不二次旋转 | UNITY_ADAPTATION，保留原唯一写入权 |
| 冻结结构观察 | 本轮原生实机未取得 | 冻结显示面后用独立相机侧看；不冻结整个Root/重播放动作 | UNITY_ADAPTATION |

## 可重复地表角点选片

位值：图片左上1、右上2、右下4、左下8；0不绘制。基于原PNG角点alpha测量，三类地表15个映射全部通过转换合同，绝不是从原生选边代码推得。

| mask | 原XML region（variant为空） |
|---|---|
| 1 / 2 / 3 | 18 / 19 / 03 |
| 4 / 5 / 6 / 7 | 21 / 22 / 05 / 07 |
| 8 / 9 / 10 / 11 | 25 / 02 / 27 / 04 |
| 12 / 13 / 14 / 15 | 09 / 10 / 13 / 01 |

森林底层full01，草地/泥土以这些真实裁片覆盖可组成带原作笔触的交界。所有region仍留在本机供核对；公开配置壳不可因缺本机媒体就以纯色/灰块伪造完成。

## 本地媒体与依赖

完整本地manifest：`E:/WORLDCREATOR/XingHaiHuiLang/研究/饥荒地图与视角/DS-MAP-01/03-converted/asset-manifest.json`。每个sheet的frame矩形采用底左坐标，pivot是相对于该frame cell的底左像素；以frameWidth/Height归一化，不能拿整张sheet宽高归一化pivot。帧合并仅累加连续重复帧时长，源动画总长不变。高树sheet为4096²，导入不能无意缩放。

本机转换入口与119项合同在研究根`tools/`。参考工具源码许可证与受限媒体分开：工具许可不赋予游戏素材再分发权。正式角色不反向依赖MapResearch或原作素材；研究媒体、原作Lua/原始代码、派生图集、录屏/构建均留本机，不进入公开GitHub、默认构建列表、Resources全量入口或正式Addressables组。

## 验收不能互相替代

- SOURCE_VERIFIED：相机参数/旋转符号、逻辑格4、连续摆放、defaultBillboard、素材/依赖/碰撞来源。
- 转换已验证：真实PNG完整合成与119/119合同，不代表Unity交互。
- OBSERVATION_BLOCKED：原作两次启动退出，无八角度动态对照；原因未知，不改安装/存档修复。
- Unity场景、角色旋转/攻击、遮挡、冻结结构模式、生命周期、保存重开和旧场隔离，必须由本轮真实Unity证据判定。
- 用户视觉与架构审查始终待用户，不由技术通过代签。
