# ROADMAP03 实施计划

用户已授权附件S9-S13，按五个独立Gate连续实施。基准e6c1e9e；附件bf0ff97之后只有伙伴接口归档。ChatGPT引用不可见，以附件和代码为准。

| 当前能力 | 增量 | 唯一所有者 | 保护 | 未核 |
|---|---|---|---|---|
| Release单批、Health、Posture | 防御快照/Guarded/Evaded | 原Release/Health/Posture | 旧场/Prefab/meta | 原作全技能未知 |
| 单Frame/单Move | 体力/闪避 | 每角色Stamina/Dodge，Root推进 | 原InputSettings/输入映射 | SAMPLE手感 |
| 原主手两段 | 独立左手盾/炸弹 | 本体Equipment/Offhand | 旧主手资格 | 正式装备UI不做 |
| 同场返场 | 同实例体力补满，装备/CD/Y保留 | 原Respawn与场景瓶 | 旧场不装新能力 | 跨图持久化不做 |

- [x] S9：先执行DefenseTests，旧真实Resolve导致Guarded HP90/100的RED已记录；新增DefenseSnapshot与AttackContact，旧Resolve转同批新入口；网关统一目标筛选、防御/反馈/姿态；链回执仅Applied。纯10、真实接触及旧链/姿态回归后发布。
- [x] S10：StaminaState验证去重支付和暂停；可选CharacterStamina在Root Request前核费、Accepted支付。DodgeState由Root推进，Movement唯一Move读取其速度；新场Inspector配Shift，输入松键重武装。返场同实例补满；世界输入/墙/窗口验证。
- [x] S11：本体Equipment保存独立实例，ShieldGuard消费RMB及体力；ICombatDefense冻结真实XZ正面判定；批次回执磨损，0HP架势折半。无左手不影响主手。
- [x] S12：Offhand独立Accepted/Release/CD时钟；鼠标地面/LOS/范围先核准再付款；派生抛物线由本体推进并在落地使用同Gateway，不调用Health。主控CD冻结政策与在途生命周期分开验证。
- [x] S13：场景HealingBottleService唯一Y输入/余额，Health.Heal夹紧且不复活；原会话成员同事务+100；新场篝火首次脱战补1。自然输入组合及直接影响矩阵，保护SHA审计，逐源行号/哈希审查文档；最终GitHub发布停止。

实际程序集依赖要求DefenseSnapshot与AttackRelease同在CharacterCore；不能从CharacterCore反向引用CombatCore造成循环。新独立场由干净ROADMAP02复制。数值均SAMPLE，技术通过不代替人工手感/架构批准；正式敌美术仍ASSET_BLOCKED。新输入/能力仅新场装配，旧场序列化保持。
