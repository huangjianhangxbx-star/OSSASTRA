# AUDIO-M02 验证

证据目录 `Candidates/AUDIO-M02/Results`；脚本在相邻 Tests，可在MCP execute_code重放。接口与核心测试不等于正常试玩，源状态不等于混音后输出，自动验收不代签听感。

已执行：核心47/47、真实Mixer/池32/32、事实边界9/9、根生命周期3/3、HUD/重复观察者7/7、Monitor十次开关稳定。原BGM Core17/Mix12/Transport16/Host5/重复根合同均通过。23为原创诊断场复用旧故事，平静行的规则仍受已知100BPM两小节hold约束，不能把该行失败称原calm失效；24在原BGM场原配置实际12/12通过。

实际混音后输出（16）：BGM Master1 RMS .0108848453、半量 .00542540187，比 .498436；SFX Master1 .00908043254、半量 .00454884768，比 .500951；Master0两者均0。音源始终38（BGM6＋SFX32），同歌身份/起播数不因调音变化。Editor全局静音本轮实读false且原样保留。

自然输入故事22：左摇杆移动、RT普攻、East方向闪避、LT合法格挡、两角色Q/E、Y治疗及Z切控均经过原绑定，无绑定override/InputSettings写入。Player/Companion/Enemy/UI真实路由；46声起播/46归还，视觉计数release12/hit14/motion3/healing1保留。21秒追加受击窗口190→190未观察到新攻击，应保留 NOT_OBSERVED_OR_REJECTED；8.568秒实际敌释放→8.574秒队友HP100→80及Companionhurt声部8实际播放已证明受伤正例，不能因后续窗口失败撤销或伪造该证据。全轮hurt13=敌12＋队友1。

27是原库存真实消费/投递API探针：2→1、飞行→结算成立，但落点(3.3,0,-2)未命中敌人，因而只有release没有hit，保持原始两项未通过，反而确认没有按消费伪造命中。它不是自然按键；有效目标补测另列，不把 miss 改写成 hit。

| ID | 判定与证据 |
|---|---|
| T01 | PASS-EDITOR-SOURCE；15重复根、25真实Single场循环，唯一根/38源稳定 |
| T02 | PASS-EDITOR-SOURCE；同根BgmTransport/SfxPlayer，12/16/22 |
| T03 | PASS-EDITOR-SOURCE；28原七路径15断言/7次Accepted，零失败；交战模块SHA保护，音频仍被动读取 |
| T04 | PASS-EDITOR-SOURCE；24 home/far实际区域同key/epoch/transport/start |
| T05 | PASS-EDITOR-SOURCE；12/15/24对话.45仅音乐，SFX用户组不受duck |
| T06 | PASS-PURE＋PASS-EDITOR-SOURCE；15 Transport16、24真实Stop/Resume/DSP及晚到合同 |
| T07 | PASS-EDITOR-SOURCE；22实际RT接受/释放；13 Accepted本身静音 |
| T08 | PASS-EDITOR-SOURCE；22 Q/E正命中/HP下降和真实短音；13 false回执静音 |
| T09 | PASS-EDITOR-SOURCE；22 t8.574队友100→80/hurt8，13无HP变化不发声 |
| T10 | PASS-EDITOR-SOURCE；22格挡/闪避/技能/瓶/跳落，27投掷miss不假hit；29公开目标API正例6/6，消费2→1、真实飞行/HP600→555、release/hit两声（非自然按键投掷） |
| T11 | PASS-EDITOR-SOURCE；13精确不可变事实重复观察静音、能力拥有释放排除根；22真实链 |
| T12 | PASS-PURE＋PASS-EDITOR-SOURCE；11子击合同/13同父动作两正回执各保留，22连续技能 |
| T13 | PASS-EDITOR-SOURCE；22两角色/伙伴独立source和实际组，切控不覆盖SO |
| T14 | PASS-EDITOR-SOURCE；22真实敌释放/命中走Enemy，与Player/Companion分开 |
| T15 | PASS-EDITOR-SOURCE；14禁用重启、25场景换代，645保护路径无玩法文件差异；不另造死亡/HP写入 |
| T16 | PASS-PURE；11非法ID/目录/clip/finite/pitch/bus/并发等显式拒绝 |
| T17 | PASS-PURE＋PASS-EDITOR-SOURCE；11精确fact、world/source代次，13合法多击 |
| T18 | PASS-PURE＋PASS-EDITOR-SOURCE；11预算/优先快照、12真实抢占；本轮立即Stop无淡出占位 |
| T19 | PASS-EDITOR-SOURCE；12固定池、22的46/46归还、25换场38源 |
| T20 | PASS-EDITOR-SOURCE；26旧VC02B独立启动本无根，bootstrap唯一38源且不擅自启歌；11/12缺媒体/错路由拒绝 |
| T21 | PASS-EDITOR-SOURCE；21保护645路径；生产播放调用只余BgmTransport与SfxVoicePlayer |
| T22 | PASS-EDITOR-SOURCE；22真实视觉计数、原Arc与LineRenderer参数保留；20关HUD不停止声音 |
| T23 | PASS-OUTPUT；16实际两类输出Master1/.5/0，无双乘 |
| T24 | PASS-EDITOR-SOURCE；12实际Mixer读回BGM/SFX/Enemy等独立因子，16实际Master输出 |
| T25 | PASS-EDITOR-SOURCE；12兼容完整增益、22偏好不变，旧三个键保持 |
| T26 | PASS-EDITOR-SOURCE；12快照/焦点/静音、14/15精确撤销；22 InputSettings/Editor静音/偏好不变 |
| T27 | PASS-EDITOR-SOURCE；24 TimeScale0同DSP继续；业务适配器在无新事实时不凭输入补音 |
| T28 | PASS-EDITOR-SOURCE；25真实Single切换旧租约/请求失效，BGM旧已开始内容保持 |
| T29 | PASS-EDITOR-SOURCE；17打开关闭10次、38源/4租约/0token不增 |
| T30 | PASS-EDITOR-SOURCE；22约45.5秒正常绑定故事，输出证据16单列；不称实体手柄手感已签 |
| T31 | PASS-EDITOR-SOURCE；21非目标SHA不变，36原dirty保留，新媒体数学原创且清单定向发布 |
| T32 | Editor通过；30一次Player探测3.53秒，旧Unity-Favorites-Tool的14错误，PLAYER_BLOCKED_EXISTING_PLUGIN；GitHub精确SHA见handoff与发布回执；PENDING-HUMAN-LISTENING |

正式人耳听感、实体手柄长时体验、完整Voice/环境/原作全部音效仍未验收。未观察到的附加窗口与真实阻塞不得写成全绿。
