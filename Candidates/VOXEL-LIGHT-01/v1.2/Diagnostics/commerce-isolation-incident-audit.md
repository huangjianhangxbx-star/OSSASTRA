# Commerce 隔离异常审计记录

首次Editor隔离命令因Vector3序列化失败，随后误进入Play约30秒，原Commerce目录写入了checkpoint。初始四文件hash与异常后hash不相同，不能声称本轮原存档全程未变化。

`editor-isolated-commerce` 最初由原档复制，但后续已被用作隔离Play写入目录；审计期间观察到其中revision与装备时钟继续变化。因此它不是不可变的事故前内容，不能用它与 `isolation-incident-after` 的语义差值证明事故只改变了序号/时长，也不能证明事故没有影响库存或世界进度。

固定JSON完整保留了审计时读取的两个目录的文件hash、封套与JSON逐字段差异，但其中 `before` 只是当时的目录标签，不能解释为事故前状态。该次目录对比可见的变化限于会话、checkpoint序号、状态revision、模拟/装备时钟，以及商人刷新revision/下一刷新时间；库存物品、装备耐久、冷却deadline/已接受操作集合、world containers、热栏、合成与world-props章节在该次读取间相同。这些事实不消除上述基线限制，亦不构成对事故影响的保证。

没有进行存档恢复、回滚或覆盖。没有操作Unity。

异常后四文件位于 `isolation-incident-after`，其两个OCS1封套均通过SHA256(body)校验；该目录应保留不可变，后续Editor/Player验证将原档与该目录hash比较，以证明后续隔离没有继续写原档。初始hash仍保留于 `p0-save-hashes.json`。

完整机器可读证据：`commerce-isolation-incident-semantic-audit.json`。
