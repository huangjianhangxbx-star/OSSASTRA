# Player retention gate：材质缓存最小修复

Root 实际首轮 Player 失败保留在 `artifacts/LOOKDEV-01/SurfaceLighting/PlayerRuns/20261110-083245-855-evidence/evidence/player-evidence.json`；没有将 Editor 资产路 12/12 或实机图替代 Player 通过。

已读取 Root 的实际 Native 最小复现 `retention-property-minimized.json`：同一 wrapper（F0/O6、shader 声明索引 roof0/full2）新材质 HasProperty 为 true；CopyPropertiesFromMaterial(vendor) 后这两项变 false，但 Shader 声明、Pass 与 keyword 数29保持；同 shader 重赋后 HasProperty 恢复 true。_N_F_O 在三种情况均存在。早先 transient fixture 的三项正例失败因而有一个真实可复现的检查缺陷，不能继续当成仅测试夹具无关紧要。

假设排序：1）材质缓存造成 Room 自定义属性假阴性，已用单变量复现；2）Player 实际 Pass 缺失仍需运行时日志排除，编译日志出现 Pass 不等于某材质实际可调用；3）精确变体被剥离须由第二次 Player 的实际 shader/keywords/图像排除，当前 general gate 错误本身没有证明这一项。

候选仅改变已证假阴性谓词：在精确 wrapper shader 上用 FindPropertyIndex 检查两个自定义声明。shader 对象/名称/isSupported、Forward/Outline、源 tag、精确 keyword signatures、重复/缺失拒绝、_N_F_O 与 SRPDefaultUnlit enable 均保留；不改 wrapper、场景、retention资产或后端。失败详细记录材质名、实际shader、F/O及两个声明索引，便于 Player 继续区分风险。

生产 base A772119CEBAE96102CC9002DFDF81A897799158F61EBEE91B96A4AA6001FE43C；候选03755E48A6FE5BD82044CC76E3F0D5F391B4C35039279BB21E85271E75E60F6C。外部实际引擎 DLL 编译0错误0警告。Root 复跑旧 transient RED→导入→GREEN，再执行第二次 Release；Native GREEN 与 Player GREEN 未收到前不宣告已修复 Player。

Root 已完成区分所需的最小 Native 探针，因此子代理没有重复操作 Unity、创建新 probe 导入或重新生成已成功 retention 资产。
