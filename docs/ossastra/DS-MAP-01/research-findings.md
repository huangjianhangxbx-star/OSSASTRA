# DS-MAP-01｜单机原作定向研究

2026-10-29。本报告区分本机源码证据、Unity等效适配与未取得的实机证据；不将计划或转换退出码当作可玩场景验收。用户本轮改为滚轮/方向箭头旋转，覆盖计划中的Q/E，正式技能键不占用。

## 资料身份与来源保护

本机源是 `D:/steam/steamapps/common/dont_starve`：单机 Steam app219740、manifest build12527201，已有游戏日志报告 `578406 WIN32_STEAM / release`。DLC app282470、393010、712640已安装；实际世界启用组合未知。本研究只采用base `data/`，未以DST源码、DLC或mod覆盖替代。

安装、原归档和既有存档保持只读。原作两次启动未获得可控窗口且进程退出，原因未知，记 `OBSERVATION_BLOCKED`；没有修安装、改存档或安装mod。原作八方向视觉一致性未完成，不能由Unity技术样板代签。

本机完整证据根：`E:/WORLDCREATOR/XingHaiHuiLang/研究/饥荒地图与视角/DS-MAP-01/`。

- `00-baseline/game-identity.json`：Steam清单/EXE哈希与本机身份。
- `00-baseline/source-index.json`：22个定向Lua/Shader证据文件，以及18个选定媒体的SHA-256和ZIP条目哈希。
- `01-research/research-findings.md`：完整本机调查；`props-contact.png`及三类地表接触表只留本机。
- `03-converted/asset-manifest.json`：源映射、PNG哈希、帧率/帧矩形/时长、root pivot、比例与碰撞依据。

公开报告只包含自有研究表述和经过核对的路径/元数据，不嵌入原作图像、原作源码或可还原的动画几何。

## 相机与旋转：SOURCE_VERIFIED

`data/scripts/cameras/followcamera.lua`：构造函数4–11、SetDefault14–48、Apply116–143、Snap191–205、Update216–311。

| 项目 | 本机源码值 |
|---|---|
| 初始水平观察角 | headingtarget45° |
| 观察目标偏移 | (0,1.5,0) |
| FOV提交值 | 35° |
| 默认距离/限制 | 30；15–50 |
| 缩放距离步长 | 4 |
| pan/heading/distance gain | 4/20/1 |
| 俯角 | 随当前距离在30–60°间线性变化；默认约42.857° |
| 旋转对象 | 相机位置、方向、up向量；没有把世界物件根整体旋转 |

`components/playercontroller.lua` 的 RotLeft211–224 将目标角减45°，RotRight227–240 加45°；DoCameraControl1031–1048 长按每0.25秒重复，同帧左优先。最终键盘默认绑定在本轮可见Lua中未定位，不能把常见Q/E键位冒充本机证据。

FOV支持透视解释，但实际投影矩阵由原生引擎建立，未读取/实测，故确切原生投影类型仍为UNKNOWN。Unity选透视FOV35属于明确的等效实现。源GetRightVec/GetDownVec56–62使用目标角；Unity在平滑过渡中使用实际相机姿态，是为避免输入与画面不同步的适配选择。

## 地形逻辑格与连续物件

- `constants.lua:5`：TILE_SCALE=4。
- `map/graphnode.lua:84–126 Node:AddEntity`：逻辑格减地图半宽后乘4，可加每轴[-1,+1]连续随机偏移，再取到0.01并保存x/z。地形格和物件摆放点不是同一约束。
- `components/placer.lua:46–72`：鼠标默认世界坐标；只有snap_to_tile或snap_to_meters才吸附。`prefabutil.lua:1–38`接收此配置；所选木箱/火坑placer没有传snap。
- `map/static_layout.lua:47–88`另有Tiled像素换算；这不是纹理重复周期或运行时加载区块。
- `map/forest_map.lua:238–285`执行表是tiny150、small250、default350、medium400、large425、huge450，生成后重新GetWorldSize。`map/customise.lua`里的旧尺寸注释不是此执行表，不据此宣称用户当前世界尺寸。

`worldtiledefs.lua:3–10,84–111`把grass/forest/dirt图集与噪声纹理分开配置；`shaders/ground.ksh`可读部分把原图颜色乘世界XZ重复噪声并输出alpha。原生邻格选边规则和noise周期常量未取得。

实际解码的grass是2048×1024/80区域，forest2048×1024/85区域，dirt1024×1024/48区域。它们含真实不规则透明边缘，不是噪声图或纯色块。图集UV来自安装内同名XML；完整213个裁片只留本机。

## 场景物件的显示与碰撞

`constants.lua:588–594`定义Billboard、OnGround、RotatingBillboard，Default=Billboard。所选prefab没有SetOrientation覆盖，因此默认Billboard有源码支持；原生是否完整俯仰对齐、仅竖轴对齐及透明深度状态仍UNKNOWN。

| 类别 | base prefab | bank / build /选定clip | 源障碍半径 |
|---|---|---|---|
| 常青树高、矮 | evergreens.lua3–6,504–524 | evergreen_short / evergreen_new / sway1_loop_tall、sway1_loop_short | .25 |
| 树桩 | evergreens.lua的stump映射 | 同build / stump_tall | 根与图片独立；样地碰撞按明确配置 |
| 草 | grass.lua3–4,64–76 | grass / grass1 / idle | 无obstacle |
| 树苗 | sapling.lua3,45–52 | sapling / sapling / sway | 无obstacle |
| 浆果丛 | berrybush.lua131,144–155 | berrybush / berrybush / berriesmost | .1 |
| 岩石 | rocks.lua3,76–80,122–124 | rock / rock / full | 1.0 |
| 木箱 | treasurechest.lua5,88–99 | chest / treasure_chest / closed | 所选构造没有obstacle |
| 石火坑 | firepit.lua5,55–68 | firepit / firepit / idle | .3 |

火坑本轮是无火状态；燃烧火焰是另一个FX依赖，未提取，不称已迁移燃烧表现。选定动画facing byte均255，共用同一完整物件图像，不是八张独立方向页。

树/岩石/浆果丛/箱含snow可选符号。`standardcomponents.lua:311–320 MakeSnowCovered`另行OverrideSymbol且非冬Hide。本轮只明确排除该季节依赖；其他未知缺符号会使转换失败。没有以漏图静默冒充成功。

BILD顶点/UV和ANIM仿射、层次及根原点共同决定完整物件。转换采用整段共同画布与原根pivot，避免逐帧裁剪居中导致脚点漂移；树冠图片尺寸不决定碰撞占地。

## 实际转换与复现入口

本机未找到现成ModTools、ktech或krane。只下载和阅读可信作者源文本，未运行下载二进制或安装全局Python。

- [Klei官方Mod Tools](https://github.com/kleientertainment/ds_mod_tools)：编译管线参考，不当作一键反解器。
- [ktools作者说明](https://github.com/nsimplex/ktools/blob/a1d1362bdb2b9aa9146d7177fbf0e351eab414ba/README.md)：TEX与ANIM/BILD转换；同提交serializer/specs用于核对本机版本。
- [ktools许可证](https://github.com/nsimplex/ktools/blob/a1d1362bdb2b9aa9146d7177fbf0e351eab414ba/NOTICE.txt)：GPL-2.0-or-later，部分依赖另有许可。参考源及本机薄工具不并入正式角色代码。

研究根下`tools/selected_assets.py`与`export_selected.py`使用现有Pillow12.3.0/NumPy2.3.5，精确读取KTEX/BILD6/ANIM4，按BILD三角UV和ANIM变换/层次合成。选定源18文件共8,698,847字节；源副本+转换结果约23.1MB（22.03MiB）。没有人物、音频、海洋、洞穴或DLC全量提取。

9个视觉变体对应8类物件，树高矮各完整79帧、草84帧、树苗80帧；连续重复静态帧只合并时长。导出动画坐标1/3以匹配原图集采样级别；PPU100是Unity样地校准值，不宣称原生每米像素数。高树图集4096²，必须避免导入时缩成2048导致frame rect失效。

转换合同`tools/validate_selected.py`实跑119/119：白名单、源/产物哈希、完整时长、帧范围、pivot、可选snow、地表角点映射、半透明边缘、非对称仿射、共享三角边不重复混色和一次PMA处理。实际PNG已打开检查完整合成；这些不替代Unity真实交互或原作实机对照。

## 核验摘要哈希

完整本机索引保留全部SHA-256；以下便于复查固定基准：

| data相对路径 | SHA-256 |
|---|---|
| scripts/cameras/followcamera.lua | 8a41fe80ffb9b25d598927c9e0223443cde6c7988287fe0db5dd42afe340b4a6 |
| scripts/components/playercontroller.lua | 72e9c8b3f9c1d9225f52d7813c9cbbd72b76d0b0ba5445116850e6c969edb5ef |
| scripts/constants.lua | 8bfad233f9cba17161671c18c8b937312577a5fa246e186534cb3df6c501db38 |
| scripts/worldtiledefs.lua | d3e67f67796f3606a9f7433554606adde0792d90f3adf9a3566c74d0f5429161 |
| scripts/map/graphnode.lua | 14d5d139dcee1ae718bb9533174f3a517b33d0112a0101b1a45734b614b116e4 |

## 保留的未知

原生投影/Billboard矩阵、透明深度/影子合成、原生自动选边、noise实际周期、当前世界DLC/mod状态、原作八方向实机对照仍未证明。人物继续使用星骸现有角色与原控制链；Unity技术验证与用户视觉/架构验收分别记录，不把两个状态压成“全部完成”。
