# VOXEL-MAP-01 交付记录

当前验证主场仍为 `Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/SC_RoomVisibilityLab.unity`。已先在独立 `Assets/OSSASTRA/VoxelTerrainValidation/Scenes/SC_VoxelTerrainHousing_Validation.unity` 放行，再回替主场，保留45°镜头和原双角色切控。

## 本轮交付

- 既有星骸地图工坊可编辑母体：.25m格、150760体素、3086.75平方米圆块地表；严格冻结源接收后224个合并Mesh/Collider、7608顶点、3804三角形，无逐体素GameObject或运行重建管理器。
- 原地面纹理随实际地形顶面，土侧与洼底分材质；两处小坡高.75/1.25m；一处真实-.5m凹地，304水格合为一个-.18m水面显示，不充当站立支撑。
- 先三视图、再Blender语义模型：一间木屋、两类树共六株；2m门洞、真实窗洞、8个房屋显示部件、3块独立顶盖。独立审查8.1/10，艺术返工0/6；这是原型范围放行，不是用户美术终审。
- 复用原Building/Room/Portal、整栋揭顶、前墙剔除、窗景许可、室内暖灯和遮光；旧高台及碰撞保留。15个低地道具支撑迁移；原3D装备台移至干地(12,.02,-21)，身份与业务引用保留。两株失去岸边支撑的旧装饰停用，记录可回退。
- 左上“体素地图样板”提供五个定位、总览、跟随和来源统计。定位明确是调试功能；正常移动仍用原控制链。

## 实际验证

| 检查 | 结果与边界 |
|---|---|
| Native源合同 | 有效RED后Feature12项、真实生成源6项通过；SHA、占格外表面完整覆盖、绕序/坐标、预算与材料分组验证。 |
| 面板合同 | 实际Play 5项通过，重复总览/恢复、错误接线拒绝、原导航委托及不改时间/角度。 |
| 保存重开 | 最终主场28/28通过，含两坡/洼底射线、15道具生产TryPlace、屋顶碰撞/ShadowCaster、独立高台承载、无遗失脚本及稳定缓存退役。 |
| 普通移动 | 独立场小蓝与Freycia各5/5；回替主场小蓝5/5。InputSystem真实按键→原输入→原Motor；仅各段起点/归位用既有调试定位。坡顶实际Y≈.77/1.27，进门room-timber/出门null，水洼最低Y≈-.48且可上岸。 |
| 机位/可见性 | 45°近景、全景、两坡、水岸、闭顶外观与室内/反向机位已观察；窗口外景、背墙/山墙保留，整顶及前向墙按旧规则隐藏。 |
| Windows Player | 实际主场Development构建成功，57.32秒、0错误、43条既有构建警告。可见窗口实际138个主相机帧，RoomMask Active、Unsupported=0、Error空、运行错误0，进程退出码0。隐藏窗口首轮0帧超时未算通过。 |
| 存档安全 | Play前明确隔离；Player在Start前再隔离，实际SavePath核对；原业务档4文件SHA全过程未变。测试后的作者路由已恢复原路径，不恢复/覆盖用户进度。 |

本轮在共享工作树上验证，初始42项其他tracked改动与未归属内容未提交或回滚。临时Player探针已通过AssetDatabase移除，清理后编译与28项主场预检仍通过。

## 修正过的真实接缝

透明Lit水触发现有许可后端的安全清黑，故本轮使用不透明受光水色。原场景CC跨阶.2m不足以通过.25m阶地，仅两名队员的场景stepOffset设为.3，未改Prefab默认或Motor。木屋显示容积扩大以包含墙/地板，建筑进入容积保持不变。唯一Prefab道具的支撑迁移补记override，解决重开回到旧底板。旧高台缓存叶会合法重建，回退改存稳定Binding/useCache开关，物理cap独立保持，不保存易失Renderer引用。

## 真实限制和本机证据

水面暂无透明、折射、波纹、浮力；地形为静态烘焙，没有运行时挖改。模型较参考简化，.25m格状岸线明显。安装候选是固定源的一次性迁移，已有安装标记不能重跑，也不能当作未来增量重烘焙器；改源须新版本并重新做完整源/网格核验。总览退出统一回到角色跟随。

源SHA：`f4635f08f266280976712561872b72fb0329cb1b40223684fa9a529c04afcbf0`。原始母体、Blender/FBX、参考、截图、进度与Player留在本机，R11只定向发布自有源码/配方/测试/文档。

本机证据目录：`E:/WORLDCREATOR/XingHaiHuiLang/研究/VOXEL-MAP-01` 与项目 `artifacts/VOXEL-MAP-01`。最终报告包括 `walk-blue-final.json`、`walk-freycia-final.json`、`walk-main-final.json`、`main-preflight-final.json`、`player-visible-20261107-1238/result.json`；失败首轮保留，不混写为成功。作者操作及回退见 [authoring-and-rollback.md](authoring-and-rollback.md)，独立审查见 [model-review.md](model-review.md)。

用户已取消关机要求；交付后保持开机。
