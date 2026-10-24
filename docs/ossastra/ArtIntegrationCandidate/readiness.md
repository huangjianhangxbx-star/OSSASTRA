# VC02B 正式美术接入审查入口

迁移范围版本：VC02矩阵v1.0 + VC02B-migration-coverage.md明确适配。2026-10-24更新至AM01–AM03定向修正基线0b922064b217e46e274ec29b5c2349ac355aa2e1；CONFIG-01仅整理作者入口、目录与样板，不重宣称整套历史回归。

```text
ART_ENTRY_READY=true（本机Editor候选，进入用户契约审查）
FUNCTION_MIGRATION=complete（包内必需范围；DATA_ONLY/EXCLUDED按矩阵）
TECHNICAL_INTEGRATION=verified（Editor）；Windows Player=blocked
FORMAL_ART_ASSETS=not-complete
USER_STANDARDS_FROZEN=false
```

以上不表示正式产品可发行、所有第三方插件可构建或干净克隆可直接运行。Windows开发构建实际失败：既有 `Assets/Unity-Favorites-Tool` 的 `com.shw.favoritestool` asmdef未限制Editor，QuickAccessWindow中的EditorWindow/MenuItem/AssetPostprocessor等进入Player编译，14个总错误。未修改第三方插件或构建设置。它阻塞Player验证，不阻塞本机Editor的美术契约审查；要发行前必须独立处理并重跑构建。证据见player-build.json与player-build-compiler-errors.txt。

|准入|本轮实际证据|结论|
|---|---|---|
|E01范围|coverage逐I01–I20；继承A逐源54行；24普通/7装备/初始31实例分开|通过|
|E02通用接受|原AbilityRun费用/来源复用，盾10、作者6、UI57与A纯34|通过|
|E03有限战斗内容|A六技能/三根17分支10符文既有专项；本轮127纯、A真实56、旧场15|通过直接影响回归|
|E04真实物品|纯37、自然补给60、自然全灭拾回35、死亡边界21|通过|
|E05正常界面|原型源与Unity原生三尺寸实拍，实际UI57/地面47；读取真实库存/HUD端口|通过适配，非像素一致声明|
|E06独立装备Spine|双角色左右独立24、生命周期20，技术模型PROBE|通过接口技术；正式资产待接|
|E07动画逻辑|Root→StateArbitrator→注册机；Controller/ConfigManager/Composer/Handle；typedID/Router/完整快照。F01–F06及Movement证据见AM定向交接|上轮修复已收尾；本轮未改Runtime|
|E08目录映射|三模型755行、功能挂点表、slot/bone/point显式绑定、Point6|通过；未用骨不猜功能|
|E09SO关系|当前重新导出164SO、116局部PASS/48INFO/0REJECT；schema3包括typedID/Router/Override、实际场景与Prefab默认、GUID和挂点表|通过目录核对；INFO不冒称全面校验|
|E10作者配方|同类新近战6、两盾10、Spine24、消耗投弹14及自然60；坏配置最终13|通过|
|E11集成生命周期|自然47/60/35、最终域重载13、旧A56/旧场15，A213原生资源指纹不变|通过Editor；Player阻塞已单列|
|E12人工边界|README硬规则/候选/可调/待冻结分层、handoff场景及工具入口|通过交接准备|

用户下一阶段真正需要决定：正式角色功能挂点与握持约定；是否需要身体slot内部的装备穿插（当前是整体Renderer排序偏移）；正背面能力与语义路由；正式模型事件/动作时点、最终SO作者与素材导出规范。当前结构允许这些明确替换，但时点/层级变化仍需针对性回归。

本轮没有通用物品消费者、独立装备Spine或真实库存闭环的未实现阻塞。发行构建与正式美术/规范冻结不被偷换成已完成。停止在本审查入口，不自动进入ART01、改源骨骼或扩外围系统。

## CONFIG-01作者入口整理

根Inspector四导航与A/B实际引用回读13项、真实Mapping保存/重新导入/恢复及Prefab缺绑定5项、正常输入与uGUI换装19步通过；虚拟InputSystem设备不等于真人实体手柄验收。见[操作样板](ConfigurationSample.md)。当前动作保持快照/下次请求读新mix分别验证，无永久资产数值改动。导航使用真实按钮共用处理器与Selection/OpenAsset回读验证，未声称桌面鼠标实点截图验收。

历史AM01/02/03记录及[定向修正交接](../AM01-AM03-directed-fixes.md)保留。配置说明更新不能替用户认可每个具体子状态/握持/美术标准；本轮不新增接入前置关卡。Editor可开始逐模块审查，Windows Player的Favorites旧插件阻塞继续单列，未在本轮修复或重跑构建。
