/*
 * 脚本：ChainTests
 * 功能：用确定的模拟时刻验证普攻预输入、动画开放、连段记忆和命中限制。
 * 边界：只调用纯连段状态，不模拟 Unity 输入或播放。
 */
using System;
using Ossastra.CharacterCore;

internal static class ChainTests
{
    private static int count;

    private static void Check(bool passed, string message)
    {
        if (!passed) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
        count++;
    }

    private static void Main()
    {
        // 测试使用精确的二进制分数检查期限等号边界。
        var chain = new BasicChainState(.125, .25, .5, 4, false);
        var action = Guid.NewGuid();
        Check(chain.Request(0, true, true) == 1, "空闲新按下启动第一段");
        chain.Accepted(1, action, 0, 0, true);
        chain.Request(.125, false, true);
        chain.Request(.375, false, true);
        Check(chain.Poll(.4375, false) == 0 && chain.HasBuffer, "早按后重按延续保留，但动画开放前不接段");
        chain.MarkReady(Guid.NewGuid(), .4375);
        Check(chain.Poll(.4375, true) == 0 && chain.HasBuffer, "旧动作标记无法开放事件驱动接续");
        chain.MarkReady(action, .5);
        Check(chain.Poll(.5, true) == 2 && !chain.HasBuffer, "保留期限等号内只消费一次");
        Check(chain.Poll(.5, true) == 0, "消费后无自动重试");
        Check(chain.Request(1, true, true) == 2, "记忆截止时新按下可以接段");
        Check(chain.Request(1.0001, true, true) == 1, "超出连段记忆则重新第一段");

        chain.Accepted(1, action, 2, .75);
        chain.Request(2.625, false, true);
        Check(chain.Poll(2.8125, true) == 0 && !chain.HasBuffer, "旧定时跨帧按单一保留期限过期");
        Check(chain.Request(3.25, true, true) == 2, "定时记忆基于计划开放时刻");
        Check(chain.Request(3.26, true, true) == 1, "晚处理不会延长连段记忆");

        chain.Accepted(4, action, 4, 0, true);
        chain.Request(4.1, false, true);
        Check(!chain.HasBuffer, "末段不能预存下一整套");
        chain.Accepted(1, action, 5, 0, true);
        chain.Request(5.1, false, true);
        chain.Clear();
        Check(chain.Poll(5.1, true) == 0, "控制变化清除输入");
        Check(chain.Request(5.1, true, false) == 0, "资格拒绝不接受输入");

        var zero = new BasicChainState(0, 0, .5, 4, false);
        zero.Accepted(1, action, 0, 0, true);
        zero.Request(.25, false, true);
        zero.MarkReady(action, .25);
        Check(zero.Poll(.25, true) == 2, "零保留期允许同一模拟时刻先输入再开放");
        zero.Accepted(1, action, 0, 0, true);
        zero.Request(.25, false, true);
        zero.MarkReady(action, .2501);
        Check(zero.Poll(.2501, true) == 0, "零保留期不跨时刻记忆");

        var hitRequired = new BasicChainState(.125, .25, .5);
        hitRequired.Accepted(1, action, 0, .75);
        hitRequired.Hit(Guid.NewGuid(), true);
        hitRequired.Request(.625, false, true);
        Check(!hitRequired.HasBuffer, "旧动作回执不会授予预输入资格");
        hitRequired.Hit(action, false);
        Check(hitRequired.Request(.75, true, true) == 1, "旧两段组件保持空挥重开");
        hitRequired.Hit(action, true);
        hitRequired.Request(.625, false, true);
        Check(hitRequired.Poll(.75, true) == 2, "有效命中后可预输入第二段");

        Console.WriteLine(count + " chain contracts passed");
    }
}
