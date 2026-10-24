# DIR02 候选工具与合同

实际入口为 Assets/OSSASTRA/Scenes/SC_CharacterPresentationCompare.unity。参阅 docs/ossastra/DIR02-report.md。这里只放自有测试/装配源码，不含受限小蓝媒体。

Tests/*.cs.txt 通过已连接本项目的Unity MCP execute_code运行；先验证Edit/Play与自有场景，各脚本注释区分正常输入、隔离夹具、边界注入、离屏姿态和Profiler。没有把商业插件加入公开测试依赖或新装包。

工具ImportBlue使用本机已获准导出副本；BuildBlueVariant/BuildCompareScene只创建缺失候选，不覆盖已有作者配置。ConfigureRig是本轮自有候选的显式装配步骤，运行前检查对应场景。RenderDirectionFrames的写入仅Candidates/DIR02/LocalEvidence，需trusted local调用；媒体目录明确发布排除。不要把批量工具当正式用户美术编辑器。

性能两个Profiler脚本保留真实失败/不可用状态；Valid=false不是0成本。完整结果与边界见DIR02-acceptance。
