# 星骸地表工坊 · KT-GROUND-02

2026-10-30，本轮制作工具、原创样板、本机资源图鉴与教材的技术闭环与定向发布已完成。物理鼠标、Editor主窗口实拍、用户审美和手感保留未验。

## 直接使用

- Unity菜单：`Tools/OSSASTRA/星骸地表工坊`。
- 本机完整试玩场：`Assets/OSSASTRA/Scenes/Research/SC_GroundWorkshopLab.unity`，七层、双原角色、单原敌，敌方攻击默认关闭。
- 可分发原创场：`Assets/OSSASTRA/Scenes/Research/SC_GroundWorkshopOriginalLab.unity`，3072平方米、三层、192基层/32覆盖/19连通道路格、44个原创装饰，无角色或参考媒体依赖。
- 本机总入口：`E:/WORLDCREATOR/XingHaiHuiLang/研究/开拓物语地表/KT-GROUND-02/开始使用.html`。含600项图鉴、教程、绘制规范和素材模板。
- 公开手册：`Assets/OSSASTRA/GroundWorkshop/Documentation/03-tutorial/地面组合逻辑教程.html`，与绘制规范相互链接；没有本机资料时窗口自动使用项目内手册。

选原创主题新建地图，选择覆盖或道路层，用原生画笔制作；点放或按种子撒花草，再保存、另存。换底纹先建配方，选择“已有同类格”或“仅未来”，预览后提交。完整工程可点“保存并进入角色试玩”，退出后返回原作者图；纯Core包明确提示未安装游戏试玩适配。

## 架构与数据

Native Tilemap是唯一地面格子作者数据。SO定义边缘族、底纹配方、主题及装饰；地图注册限定路径和资源归属，每张地图拥有独立Tile、材质和主题快照。花草以原生实例根保存位置，地图级组件只派生脚点补偿和显示朝向。

图层锁由画笔与批量换材共同消费。换材只迁移已识别引用，拒绝未知Tile和过期预检；主题采用新快照，保存/关场/重载/Play都会释放临时预览。已解锁手工矩阵保留在Native Tilemap，专用Tile兼容Tile Palette的Undo刷新，不维护第二套持久格表。

“仅未来”会新增独立变体层；单层单材质不能假装旧格与新格同时使用两张底纹。装饰显式生成，有稳定种子、道路避让、间距、局部范围、手摆和锁保护，不在Update或旋转时重新随机。

## 已验证

| 范围 | 真实结果 |
|---|---|
| 原创族/地图定义 | 28项，两族各256邻域，三层注册与独立创建 |
| 换材/另存 | 核心20、另存19；两轮真正关场重开，颜色/flags/有效矩阵与装饰归属保持 |
| 原生编辑 | SceneView真实Down/Drag/Up等86项；窗口归属13、帮助路由7 |
| 装饰 | 主流程17、边界9，手摆/锁、稳定范围及脚点 |
| 图鉴 | Load600，代表导入114项检查、非法预检9项；二次导入不改旧文件，不代表114个素材或600项全部进场 |
| 角色与返回 | 参考输入24、原创交战7，Current专属Applied回执；三次试玩返回clean且原图SHA/Map ID保持 |
| 旋转 | 原创与最终参考各8稳定角+1过渡角；最终43参考草采用地图私有Yaw配置，原导入定义/脚点不动 |
| Editor耗时 | 12格API笔画11.58ms，42次局部刷新0.094ms，基线保存623ms；仅本机单次有界测量 |
| 独立性 | 25源独立Editor编译0错误/1条旧Sprite导入API警告，排除172个非白名单DLL |
| 最终Editor | 参考场Edit/ready/clean，可写，MissingScript 0，预览/会话/临时资产0，两个Shader无消息，Console无error |

详见[验收记录](validation.md)。性能采样仍有角色模拟和装饰；请求隐藏后仍有3个角色Renderer被原系统重新启用，不据此宣称纯地表CPU或纯地表渲染成本。GPU、Player、空工程导入、任意插件组合及长时无闪烁未认证。

## 交付与依赖

本机包位于总入口同目录的`06-delivery`：

| 包 | 字节数 | SHA256 |
|---|---:|---|
| `GroundWorkshop-Core_ba314e638528a8e8.unitypackage` | 2157793 | `36b662d0586c72fc28849877ab4177579231b60de27047c5223dba172a461740` |
| `GroundWorkshop-Source_ba314e638528a8e8.zip` | 2043911 | `ea1866d3b70bd7c816da66681277280714837ad008dda4530145884391cae8db` |

原生包严格83个自有文件；83数据字节与Native计划一致，83份meta仅由Tuanjie原生导出器将编码GUID写为AssetDatabase GUID，其余字节相同。源码ZIP原样取Native导出内容，未手工改工程meta。含25 Core源码（含复用的FrontierGroundBrush）、2 Shader、22原创模板、完整主题、原创场依赖、教学配方/PNG与21份手册内容。排除旧样板Builder/旧刷地窗口、可选玩法适配、参考场、原作规则数组、图鉴与参考媒体。

已用Tuanjie 2022.3.62t16、URP14.2、2D Tilemap/Extras3.1.3、Newtonsoft3.2.2验证。导入其他项目需先安装这些依赖，保留Editor目录；不要把编译验证DLL放入Assets。原作精确Shader、UV、光照和编辑器仍未知，自有world-XZ采样/裁剪/工具属Unity适配。原创技术图不代表正式美术。

本轮不关机、不休眠，原37项dirty及旧场保留。GitHub仅定向发布自有内容，不同步Gitee、不强推。产品`8cba9ffcd4de3e4ac566131d0094ad218326f8a8`已与GitHub main精确核对，全部租约释放，见[最终交接](handoff.md)。
