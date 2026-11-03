# 本轮验证入口

先读`docs/ossastra/RENDER-INTEGRATE-01/交付.md`。Native契约通过项目Funplay execute_code运行；先确认项目、场景、模式，脚本中的目标ID是本轮现场记录，不能在另一个编辑器会话盲目重放修改脚本。角色与GPU合成/深度契约会清理自有测试对象；室内定位夹具必须正常Restore，且有60秒超时恢复。

NativeCleanupAudit/Apply保留精确删除过程；Apply是已经执行过的有状态动作，不能当成可重复测试。删除资产已在Assets外备份；恢复应经Unity AssetDatabase导入，不手改场景/meta。

`protect.py`检查原dirty和指定源/存档SHA；授权主场Renderer配置为唯一显式豁免。`publish.py`只准备明确源码、候选和文档索引，保护原dirty同步前缀，不自动提交/推送。bin/obj、机器编译输出、截图、原始JSON回执和Unity配置资产不在发布清单。
