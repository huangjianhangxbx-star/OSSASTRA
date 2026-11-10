# LOOKDEV-01 v1.2 最终 Player 独立复核

2026-11-10；只读核对最终证据，未操作 Unity、修改生产或再次构建。

证据：`artifacts/LOOKDEV-01/SurfaceLighting/PlayerRuns/20261110-091636-310-evidence/evidence/player-evidence.json` 及其六张原始 PNG。JSON 实际 completed=true、status=Complete、errors=[]；A_before80帧、B145、C210、060诊断245、098诊断280、A_after345，均同1600×900、45°/45°机位。属性恢复条目 restored=true，原来没有 threshold MPB override，完成后仍没有。

## 颜色通道与辅助通道分开

|证据|独立核对结果|可说明/不可扩大的范围|
|---|---|---|
|登记源槽|17 registered slots，B/C状态为RealToon；Root的registeredColorSlots属于CPU源材质/cache登记|不能据此写17个都在屏幕可见或都已观察到Room颜色draw|
|当前相机颜色adapter|B、C、060、098每次均14个不同的 `[Room visibility draw adapter]`；正确 `Hidden/Ossastra/LookDev/RealToonRoomLocal`，supported=true；Forward=0、Outline=4|这14个是实际运行相机调度层颜色adapter观察，不是GPU指令捕获|
|关键词|逐槽比较sourceKeywords与上述颜色adapter的完整keyword集合，四次皆 missing=0、extra=0、bad=0|说明观察到的配置身份一致；不能单凭keyword数组证明所有编译分支执行或全部17槽已可见|
|其余3槽|两块复制石网格及复制金属观察球没有Room颜色adapter观察|可能是Full/vendor-direct或不在此Room调度路径，不能无依据计为fallback，也不能冒充已观察到的Room颜色draw|
|辅助truth|C有25条 `[Room positive truth]`；shader为 `Hidden/Ossastra/Rooms/PositiveTruth`，有的附 `_ROOM_STYLE_COVERAGE`|这是合法可见性/覆盖辅助通道；不要求RealToon的Forward/Outline，不能混入颜色fallback统计|

Player的实际F0/O4与Editor F0/O6不同，其他未用Pass裁剪后索引会变化；此处按实际Pass身份验证，不要求硬编码旧索引6。颜色adapter中没有观察到错误shader或静默fallback。以上是明确范围的实际Player证据，并非17槽全部GPU分支认证。

## SelfShadow 060 / 098：不确证，不计通过

已直接查看两张原始图，并逐像素计算 max(|RGB060−RGB098|)，坐标以左上为原点、矩形右/下界不包含：

|区域|差异结果|
|---|---|
|Root广投影矩形 [419,110,846,425)|2117像素任一通道不同，1527像素最大通道差>2|
|木构候选 [555,155,675,270)|0差异，最大差0|
|屋面候选 [520,235,660,310)|0差异，最大差0|
|去掉角色的较紧矩形 [419,110,787,352)|22像素非零，18像素差>2，分布零散|
|广框内最大8连通变化块（差>2）|1265像素，bbox [756,361,831,425)，对应小蓝身体/轮廓动作，不是屋顶|

广框包含角色、草与背景，所以1527不能当作屋顶受SelfShadow阈值影响的证据。当前两个静态木/屋面区域完全相同，剩余零散差异与动态草/人物/后处理变化无法严格分离。这组诊断已正确执行并恢复，但**不足以确证实际插件SelfShadow分支的视觉响应，也不足以判定分支失效**。本项结论为 **inconclusive / 未确证，不计通过**。

最终材料、布光、强蓝和窗灯画面可以作为候选交付；本报告不代签性能预算、全部观察规则、用户认可或所有17槽的GPU分支。前两次Player失败应继续留档。按Root决定停止此项追加诊断、材质迭代和构建，以实际成功与尚未确证项分开发布。
