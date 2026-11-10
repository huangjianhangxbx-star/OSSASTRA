# LOOKDEV-01 v1.3 ToonSurface01

用户批准附件独立材质计划并要求完成后正常关机。最新ChatGPT三轮已通过read_thread读取：核心是不用全屏滤镜、保持原图/几何，比较真实材质受光；本轮不扩张到局部雾或全场风格重构。

## 接手与所有权

- 本机main@a2d397a；原43 tracked dirty、Main接手Edit/45/dirtytrue；上一轮腰高草2022簇已保存到共享作者。远端通过旧本地代理首次连接失败，直连核对进行中，尚不写远端一致。
- Root唯一Unity/生产/Git。独占Candidates/LOOKDEV-01/ToonSurface01、artifacts/LOOKDEV-01/ToonSurface01、docs/ossastra/LOOKDEV-01/ToonSurface01；Native新场景/材质/Profiles在Assets/OSSASTRA/WorldRendering/Stylization/SurfaceLighting/ToonSurface01；新Shader源码在Scripts/WorldRendering/Stylization/SurfaceLighting/ToonSurface01。
- 共享最小扩展：LookDevFiniteToon.cs、SurfaceLightingRecipe.cs及实际Room精确适配文件（子候选确定后登记）。不更改VoxelLit原受光、Spine/草Shader、天气、后处理、原A/B/C默认和用户维护文档。
- shader子仅Candidates/.../Shader；integration子仅Integration；validation子仅Validation。无Unity/生产/Git权。

## 实施顺序

- [x] M0：保存保护SHA、Main未保存状态与v1.2真实Draw基线，独立副本与回退；不丢弃Main未保存内容。
- [x] M1：隔离ToonSurface01和四Material；三段、真实主阴影、GI/额外光与独立Spec/Rim；普通Mesh实际GPU归因。
- [x] M2：有限新档、真实Room颜色/Shadow/Depth/许可证据；日光/蓝夜暖灯同条件材质对照与运行恢复。
- [x] M3：同版Release Original/RealToon/新材质、300有效帧CPU/GPU与30秒录像、保存读回和原场恢复。
- [ ] M4：推荐/否决依据、参数/限制/截图/回退、定向发布（可用时）及释放writer，最后按最新授权正常关机。

## 约束

新草2022簇沿实际现行作者，不冒充v1.2旧38106簇成本；各材质同条件不得改密度/分辨率/阴影。GPU缺失保持Unknown。参考视频原文件本机路径未提供，只依据附件明确算法，未清节点不虚构。

## 2026-11-11 用户追加顺序授权：ToonSurface01 → GitHub → VEGETATION-01A v1.3
当前ToonSurface01继续完整M1–M3，不中断或并行抢Unity。完成上一轮后先定向上传GitHub保存，再直接开始用户粘贴的草表示法纠偏计划：近/中/远分级、非十字几何、根少顶多、顶向上/全向上法线、Y轴billboard、无投影/可控接收、全局/阵风/接触压草回弹、地表融合、独立Lab后主场局部验证。禁止把新计划误作继续调旧卡片。用户要求连续完成；关机授权延后到两轮都完成，不能在Toon交付时先关机。草计划原文须单独落盘后开始，不擅自省掉接触扰动或三档表示。新草作者/Shader变更需顺序重新登记，当前Toon仍不得改草Shader。

## M0/M1/M2 阶段进展（2026-11-11）
- Main保持加载，38根临时关闭，未Save原Main；另以Native SaveAsCopy保存MainUnsaved_BuildSafety.unity安全副本，避免构建保存提示导致现场丢失。首次构建因保存Main提示主动取消，551秒是等待提示，不是实际打包耗时；第二个修订因无ClearSceneDirtiness API在执行前编译拒绝，无资产改动。第三次唯一实际打包执行181045569c4242cfbb2cebadd7fbe674，目录20261111，选择不将未保存Main纳入构建。
- 新普通/Sight Shader、四作者模板与17槽/7对象有限分配已接。独立Renderer18追加，默认0与Main15保持。数学GPU19和运行生命周期17检查通过；实际Camera普通URP/Room同机位白天与蓝夜暖窗Original/RealToon/Toon截图已留。
- Raw Forward夹具最早9粉色恰为未覆盖清屏值；CullOff诊断有5616/9216覆盖且0粉，三对同带/连续关系通过。六绝对RGB失败因Color属性上传linear与最早fixture假设不同，独立白色项校准候选待Native；旧失败保留，不替换成通过结论。生产Shader保留2条Spec/Rim潜在未初始化警告。
- Player成本、实际bucket审计、30秒录像、最终Main归还与GitHub仍待完成；没有代签完整画面/60FPS。


## M3/M4 实际收口（2026-11-11）
Release20261111成功0error/61warning，同exe八相位300帧与16截图/30秒八步录像完成。Toon整帧GPU增量p50/p95日光.362/.163ms、蓝夜.295/.424ms，新增门通过；总帧约24–25ms，稳定60仍NotMet，CPU有效Unknown。生产Forward独立白色项12通过，旧测试失败保留；实际Room17槽/15颜色adapter、当前源输入0差异，4作者不变、Main候选引用0。
两Probe与MathShader已Native清理，Lab保存冷读17槽/4模板通过；Main38根归还、单场Edit/45/dirtytrue未Save，构建前后完整Native序列化SHA相同；32保护及2原业务源SHA相同。成功报告535.85秒含提示等待和首编，不作纯Shader编译计时。唯一Unity/源码writer现释放，待精确定向Git发布。随后按用户顺序启动新草v1.3，不在此关机。
