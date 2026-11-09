# 主场草层 Room 适配候选

本目录由只读审计子代理准备，未操作 Unity、Git 或生产文件。Root 是唯一应用者。

## 最小改动

- `RoomPositiveRenderingLayers.cs`：精确 `Ossastra/Vegetation/GrassLayer` 复用 Decoration 调度位，未知源仍拒绝。
- `RoomPositivePass.cs`：源感知选择草 Shader 自有 `RoomSight` 与 `RoomTruth`。原桶、MPB/Owner 上传、动态绘制和许可后端均复用。
- `GrassLayer.shader`：保留原 0–3 Pass 与独立 Lab 行为，只追加 4/5 Pass。顶点形变、逐簇 LOD 与 Alpha 完全调用原 `GrassVertex`、`GrassColor`；颜色沿既有艺术路线，增加当前许可与现有柔边黑幕处理。真值和标签共享同一变形轮廓、`RoomSightTruthColor` 与可选写深度状态。
- `RoomVisibilitySight.cs`：精确草源经临时表现登记时仍作为环境像素进入 WorldLook（排除人物/粒子），不授予新视野。照明角色由 Root 给每块 `RoomPresentationBinding` 明确传 `environmentRole=0`。

## 应用与验证

1. Root 对 `manifest.json` 的四个 baselineSHA256 重新核验。
2. 在应用前运行 `RoomGrassContract.cs.txt`，保存真实 RED（缺 Grass Classify、RoomPass 和世界环境标记）。
3. Root 审核、逐文件应用并编译，原脚本再次运行应 GREEN。
4. GPU/实机仍须核验：相同风时刻下 Final/Truth/标签 Alpha 轮廓，禁视域全隔离、门窗片元、屋顶预览、昼夜与重建/禁用资源归还。

## 接入限制

`RoomPresentationRegistry` 登记会让 `StaticEnvironment=false`，因此草不继承静态地表短暂退场历史。获得可视时可用现有淡入；失去视野后立即剔除，保守防泄露。本轮不擅自修改动态历史规则。

逐块独立路线使用既有 `DrawIndependentUnlit`，不耗有限动态 Lit 材质组，不通过静态桶临时换材。每块每帧仍有一次 Source→ShadedAdapter 属性复制及真值/标签提交；需以 Root 实际块数与当前主场测量为准，不能复用独立 Lab 的增量宣称主场性能。

`prepare_candidate.cjs` 只从精确唯一锚点生成候选副本，不会写生产源码；重新生成前请确认 Root 没有应用候选，否则 baseline 会变成已修复版本并拒绝锚点。
