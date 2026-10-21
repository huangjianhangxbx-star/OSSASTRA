# Dependencies

## 技术首发
团结编辑器 2022.3.62t16 / 1.10.4；场景使用内建 Transform、Camera。新 asmdef 没有外部程序集引用，旧代码启用符号默认未定义。没有第三方插件、模型、贴图、音频或字体随本次候选分发。

## 本机已验证环境（不随首发纳入）
- URP 14.2.0-t1，含既有 Graphics/Quality 指向的多份自定义 Renderer、Shader 和其他渲染资源。本机默认 Renderer 索引0；旧备用索引1引用已隔离的旧 GameRenderFeature 和未解析Feature，OnValidate 会报告 missing RendererFeatures。未修改旧配置，也未重新启用旧脚本。
- Input System 1.14.4-t1、Addressables 1.22.4-t1、Cinemachine 2.10.3、uGUI 1.0.0、TextMeshPro 3.0.9、Test Framework 1.1.33、Timeline 1.8.9-t1、VFX Graph 14.2.0-t1。实际后续闭环需要哪些包须另行审定，以上不是首发自动安装清单。
- 本地 AI 生成工具使用 file: 包依赖；另有 Codely Bridge 1.0.76 和 Funplay MCP 编辑工具。工具路径/配置不进入首发。
- Spine、RealToon、其他本机商业插件和美术资源未核验再分发权，不纳入。需要时应从各权利方取得许可并按其官方安装方式安装，后续记录确切版本，不猜测版本。

## 验证限制
目前只验证了本机已有依赖环境下的编译图和技术场景Play，没有执行干净克隆编译、独立Player构建或任何旧场景功能验收。首发不包含商用资源的替代品，不含旧Bootstrap，也不包含新正式玩法入口。

## M01 候选新增依赖与边界（2026-10-19）
领域核心Ossastra.CharacterCore没有UnityEngine或第三方依赖，独立测试需要.NET 9 SDK。Unity适配器使用CharacterController/Physics、Input System（当前本机1.14.4-t1，有旧Input条件回退）、Spine.Unity SkeletonAnimation（本机角色数据4.3.26）和URP靶材质。Spine数据版本不代表运行库精确版本已核验。

本次仅发布自有C#/asmdef、场景/靶材质及meta、测试与审查文档；用户target 1.fbx、rozeul数据/贴图/图集和第三方插件仍不分发。场景保留这些本机引用，干净克隆缺少它们；没有执行干净克隆Unity运行或Player构建。旧RendererFeatures错误仍是本机已知问题。

## 当前角色候选（2026-10-19）
角色C#现位于Assets/OSSASTRA/Scripts。新增DataBus与独立Orientation，不新增外部包。公开Prefab/场景仍引用未分发的本机Spine/模型/URP资源，无法仅靠当前仓库保证直接试玩。旧代码本机解禁仅用于配置参考，旧源码不分发，历史隔离配置不属于新控制系统依赖。

## VC01 本地依赖（2026-10-21）
使用现有Input System、Spine和uGUI，不安装新框架。Windows动态字体取SimSun、Consolas/Microsoft YaHei，不分发字体文件。本机rozeul仍为4.3.26数据；E1僵尸4.1.23 JSON在本机4.3运行库上经正确图集读取、13段姿态求解和实屏播放验证，不宣称跨版本普遍兼容。

Art/HudReferenceLocal、AudioReferenceLocal、EnemyReferenceLocal中的PNG/WAV/TXT为本地媒体接入，不进入公开提交；只提交本轮新导入meta、定义/材质引用及校验清单 Candidates/VC01/local-media-manifest.json。冻结HUD来源为原型c21d4fc，不使用本机Origin祖先86824bd的旧内容；metal系列PNG由该版本SVG按192dpi栅格化。商业插件、rozeul、Bomb.png、地块仍需原本机依赖，干净克隆不保证运行。新生成的RozeulShield_v1.png已获用户授权并随自有内容提交。
