// 为七页界面提供独立的演示数据。
using UnityEngine;

namespace Ossastra.UI.GameMenu
{
    /// <summary>Local display-only fixtures for reviewing menu layout and navigation.</summary>
    internal sealed class DemoMenuItem
    {
        internal readonly string Id;
        internal readonly string Title;
        internal readonly string Kind;
        internal readonly string Symbol;
        internal readonly string Summary;
        internal readonly string Notes;
        internal readonly string Badge;
        internal readonly Color Color;

        internal DemoMenuItem(string id, string title, string kind, string symbol, string summary, string notes = "",
            string badge = "", Color? color = null)
        {
            Id = id;
            Title = title;
            Kind = kind;
            Symbol = symbol;
            Summary = summary;
            Notes = notes;
            Badge = badge;
            Color = color ?? MenuPageBuilder.Silver;
        }
    }

    internal static class GameMenuDemoData
    {
        internal static readonly string[] AttributeNames = { "HP", "攻击力", "防御力", "恢复力", "恢复次数", "SP" };
        internal static readonly string[] AttributeValues = { "125", "72", "20", "70", "3", "—" };
        internal static readonly float[] AttributeFill = { .26f, .15f, .07f, .20f, .60f, 0f };
        internal static readonly string[] CurrencyNames = { "魔质", "废料", "残片" };
        internal static readonly string[] CurrencyValues = { "1,135", "190", "8,430" };

        internal static readonly DemoMenuItem[] Skills =
        {
            new DemoMenuItem("skill.sword", "断锋斩", "主要", "刃", "以快速横斩攻击近处目标。连续输入可接续下一次攻击。", "训练用的基础招式。锋刃划过金属时会留下短暂的共鸣。"),
            new DemoMenuItem("skill.axe", "破坏之斧", "主要", "斧", "以沉重斩击攻击近处目标。连续输入可衔接后续斩击。", "旧式工具上的纹路仍在发光，仿佛记得握住它的指尖。"),
            new DemoMenuItem("skill.punch", "盾突", "辅助", "盾", "以向前的盾击压制近处目标。", "保护同伴与打断来势，往往是同一个动作。"),
            new DemoMenuItem("skill.blast", "回声弹", "辅助", "弹", "发射在近距离引爆的回声弹。", "狭长通道会放大回声，试制件便是因此得名。"),
            new DemoMenuItem("skill.spiral", "回旋裂隙", "辅助", "旋", "向周围释放旋转的回声，短暂压制接近的目标。", "初次观测产生的环形余迹仍留在旧图纸上。"),
            new DemoMenuItem("empty", "空技能槽", "技能", "—", "暂无样例技能。")
        };

        internal static readonly string[] EquipmentTabs = { "外壳", "手镯", "辅助器" };
        internal static readonly DemoMenuItem[][] Equipment =
        {
            new[]
            {
                new DemoMenuItem("equip.shell", "旧式护壳", "外壳", "甲", "样例外壳。使用旧式插槽拼合而成。", "边缘的磨痕仍可辨认。"),
                new DemoMenuItem("equip.shell2", "微光护壳", "外壳", "光", "展示护壳的只读资料。", "表面覆有薄薄的釉层。")
            },
            new[]
            {
                new DemoMenuItem("equip.band", "铜环手镯", "手镯", "环", "展示手镯的只读资料。", "可见一圈浅浅的刻印。"),
                new DemoMenuItem("equip.band2", "缄默手镯", "手镯", "缄", "展示另一件手镯的样例资料。")
            },
            new[]
            {
                new DemoMenuItem("equip.aux", "试制调谐器", "辅助器", "调", "用于展示辅助器名称、分类和说明。", "短暂靠近电源时会发出细微嗡鸣。")
            }
        };

        internal static readonly DemoMenuItem[] Relics =
        {
            new DemoMenuItem("relic.crow", "染血铁鸦", "遗物", "鸦", "空中攻击所造成的伤害增加 10%。",
                "一件留有打磨痕迹的魔术工具。\n绕成环的旧绳几乎快要磨断了。", "2", new Color32(153, 170, 157, 255)),
            new DemoMenuItem("relic.dice", "旅人的骰子", "遗物", "骰", "展示遗物的只读效果说明。",
                "无法从磨损的刻点判断最后一次掷出了几。", "1", new Color32(193, 160, 126, 255)),
            new DemoMenuItem("relic.token", "守望徽记", "遗物", "章", "展示尚未装备的样例遗物。", "轻轻翻转，背面有一道刻线。", "1"),
            new DemoMenuItem("empty", "空遗物槽", "遗物", "—", "暂无样例遗物。")
        };

        internal static readonly string[] CollectionGroups = { "技能零件", "重要物品", "强化", "行动", "场地物资", "旧藏品" };
        internal static readonly DemoMenuItem[][] Collection =
        {
            new[]
            {
                new DemoMenuItem("collect.amplifier", "实验用增幅器", "技能零件", "芯", "用于解锁和强化技能的零件。", "旧机体上的内置装置。内部仍留有少量储能。", "0"),
                new DemoMenuItem("collect.module", "折返模块", "技能零件", "轮", "按原有形状保留的试制模块。", "每一片环带都刻有不同的标记。", "0")
            },
            new[]
            {
                new DemoMenuItem("collect.note", "刻纹记录片", "重要物品", "片", "存有简短文字的记录片。", "仍能辨认一条关于电梯维护的备注。"),
                new DemoMenuItem("collect.card", "检修通行牌", "重要物品", "牌", "标有旧式门禁识别号。")
            },
            new[]
            {
                new DemoMenuItem("collect.crystal", "淡蓝矿晶", "强化", "晶", "一小簇发光的矿晶。", "即使被分开，断面也会残留微光。", "5"),
                new DemoMenuItem("collect.vial", "澄明剂", "强化", "剂", "保存在细玻璃瓶内的试剂。", "流动时，瓶中的微光变得更明显。", "2")
            },
            new[]
            {
                new DemoMenuItem("collect.walk", "移动", "行动", "行", "展示移动动作的说明样例。"),
                new DemoMenuItem("collect.guard", "防御", "行动", "护", "展示防御动作的说明样例。"),
                new DemoMenuItem("collect.dash", "闪避", "行动", "避", "展示闪避动作的说明样例。"),
                new DemoMenuItem("collect.jump", "跳跃", "行动", "跃", "展示跳跃动作的说明样例。"),
                new DemoMenuItem("collect.dive", "俯冲", "行动", "降", "展示俯冲动作的说明样例。"),
                new DemoMenuItem("collect.run", "奔跑", "行动", "跑", "展示奔跑动作的说明样例。")
            },
            new[]
            {
                new DemoMenuItem("collect.fiber", "植物纤维", "场地物资", "藤", "可从地表收集的纤维样本。", "草叶干燥之后十分轻软。", "4"),
                new DemoMenuItem("collect.stone", "磨圆的岩片", "场地物资", "石", "湖畔可见的岩片样本。", "", "7")
            },
            new[]
            {
                new DemoMenuItem("collect.pendant", "遗失的坠饰", "旧藏品", "坠", "一枚挂绳断开的坠饰。", "坠饰背面没有印记。", "1")
            }
        };

        internal static readonly string[] InformationTabs = { "资料", "教程" };
        internal static readonly string[][] Information =
        {
            new[]
            {
                "陈旧的实验记录", "机体检查记录", "残破的信", "酒吧的布告栏", "老旧的笔记", "首席调律记录",
                "旧城区搜索记录", "矿石采掘记录", "维护员交接日志", "地表信标调查", "远岸采样安排"
            },
            new[]
            {
                "菜单与导航", "使用地图", "查看技能", "浏览收集", "切换分页", "调整音量"
            }
        };

        internal static string InformationBody(int context, int entry)
        {
            if (context == 1)
            {
                return new[]
                {
                    "用方向键在当前界面移动焦点。按确认键查看当前条目。\n\nQ／E 切换主分页。Esc 返回上层。",
                    "方向键与拖动可以移动地图。使用滚轮缩放。\n\nTab 回到当前位置，Shift 查看休息点。",
                    "将焦点移到槽位或技能分支，以查看说明和预览。\n\n技能页展示的是浏览样例。",
                    "滚动分类区域以浏览更多条目。选中条目后可在右侧显示详情。",
                    "在有二级分页的界面，用 1／3 或鼠标点击标签。",
                    "声音设置入口位于菜单底栏。设置会通过游戏已有音频选项生效。"
                }[entry];
            }

            return new[]
            {
                "从门上滑落的旧记录，纸张边缘已经起毛。\n\n清点员沿着外环完成了第二次巡视，但是所有指示灯仍然同时闪烁。\n\n下一班应从南侧阶梯进入。",
                "第一批机体的组装已经结束。\n\n从检修孔听见了微弱杂音，尚未找到声源。\n\n将所有转轴的读数抄录到表格中，安排下一次检查。",
                "我把钥匙留在了老地方。\n\n如果看到长廊的灯熄灭，就沿着石阶走。\n\n不要等最后一班出发。",
                "本周货物抵达日期推迟。\n\n需要回收旧组件的人请在黄昏前登记。\n\n布告栏由值班员每日擦拭。",
                "有关旧址的记录，只留下很少的字句。\n\n纸面有明显的茶渍，文字却一笔不乱。\n\n最后三页记录了近几年检查过的机械零件。",
                "将主要共振信号保持在安全区域内。\n\n异常样品须由两名值班人员检查，标记手写读数。",
                "地表小队对废弃城区进行了三天的搜查。\n\n地图上的旧入口大多早已封闭。\n\n拾得的物品将登记在下一页。",
                "矿道支架需要重新检查。\n\n采样带先从边缘开始，矿灯要保持常亮。",
                "从半夜开始，离开下层的梯车停止运行。\n\n交接人员须在准备间等候。",
                "四座信标都按原定次序闪烁。\n\n最远处的灯比记录中晚了一瞬。",
                "安排两名记录员留在营地。\n\n采样小组将在下午通过旧桥，归程前都需要手动确认旗标。"
            }[entry];
        }
    }
}
