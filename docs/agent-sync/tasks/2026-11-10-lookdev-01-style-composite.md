# LOOKDEV-01 v1.1 实机风格合成

- ID：LOOKDEV-01/StyleComposite（接续，不建立LOOKDEV02）
- 状态：technical-complete；独立候选交付，视觉待用户、CPU/稳定60预算未过。Unity/源码writer已释放，Root最后定向发布。
- 批准包：E490194F38DC52757385097014F81B8B402C8EE708632A677CC8340DFA3CE436。
- 路径：同TASK_REGISTRY；Main/CHAR/草原作者资产只读，受控场景副本及独立Profile允许Native创建。
- 最新ChatGPT内容已读，附件是明确执行规格，文中“只制作计划未实施”为历史制作边界，不抵消当前用户执行授权。

## 计划
- [x] M0保存真实现场、确认HDR/深度/许可/角色外缘/管线，复制当前主场作受控候选与基线。
- [x] M1原WorldLook内接主题/解析雾/四级Bloom与同帧覆盖，展示真实蓝色候选。
- [x] M2块内均值/保守许可/2及4像素/基础亮度分档，HUD原分辨率。
- [x] M3同资产墙石屋顶道具自主Toon/有限外轮廓，核实本机RealToon对照，不改商业插件。
- [x] M4同算法红主题/无像素完整候选、生命周期/门窗未知亮源/连续实录、Player与Editor成本分别测量。
- [x] 恢复Main原默认，视觉待用户与组合预算分列，释放Unity/源码writer。发布回执作为最后步骤单列。

## 验证规则
先执行包Python数学自检，然后Native接口/GPU实际许可/深度/色彩测试。记录失败、不把CPU自检当GPU；不降低Main质量或以新相机重画世界。主场既有GPU/稳定60未通过事实保留，本轮有限视觉候选不无限扩展性能任务。

## 2026-11-10 M4检查点

- Settings 11、Registry API 10、五家族MSAA1/4 MRT 62、真实合成Shader GPU 11、资源/resize 12均Native GREEN。
- Camera-only window旧label不覆盖场景合法窗景，采用明确受限的style-only许可补足，不绑定label颜色、不写深度、不重画世界颜色；上限128表面/256槽。差集为Window && !Eye；Native 70项GREEN。复制场room-a窗口1域70表面76槽，无core/style错误；关闭真实窗景后0域/0表面/0槽。
- 4实际物体14材质槽，原→自主三档→真实已购RealToon Forward0/Outline6；商业包裹仅本机LocalMedia，原插件未写。已实际同机位、关门遮板、深夜、窗景关闭检查，未知区域保持黑幕。
- 实机PNG 00～08、10位于artifacts/LOOKDEV-01/StyleComposite；正式30秒记录45aed84fb58b4f1085ee0c3e8399f3b0，388真实连续PlayerLoop帧。一次早期录屏因Editor未系统聚焦未证明点击，不作为输入通过证据；仅用computer-use将窗口置前，回MCP后strict new_input_system显示Down/Held/Up共10帧、cleanup_verified=true。未改输入/后台策略。
- Release Player一次构建正在运行：LocalBuilds/LOOKDEV-01/StyleComposite_20261110/LookDevStyleCandidate.exe；FrameTiming仅构建范围临时开启，finally恢复。不要重放构建。后续待Player及Editor成本、保存重开、probe清理、原Main恢复及定向发布。

## 最终收口

Release v2构建0错误；Player和Editor六态各180帧完成，跟踪RT回归A。GPU增量0.74–1.22ms，完整组合CPU主inclusive增量0.68–1.15ms，整帧25–26.5ms；不认证稳定60。Player逐阶段缺失，Editor红整帧GPU异常mean/max禁用。详性能采样.md。

临时Probe组件/源码/meta Native清除，13项生命周期GREEN，冷重开45°/Renderer16/零missing/三个独立Profile读回。最终Main Edit45°/Renderer15/新style=null/dirtyfalse/FrameTimingfalse；Main SHA、四作者资产和四业务源SHA守恒。30秒移动和16秒API旋转实录保留，无需前台继续操作。RealToon仅Editor，Player入口关闭；商业展开不发布。

旧自有构建目录递归删除被自动审批拦截，未提供具体原因；保留旧目录，不再重试。截图入口和保留v2试玩在交付.md。定向发布仅自有源码/meta/合同/文档；原43tracked dirty排除，具体SHA以本机publication.json回执为准。
