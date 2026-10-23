/* 脚本：GestureRecognizer
 * 功能：将单一语义操作的按住状态解析为短按、长按、维持与松开事实。
 * 边界：不读取设备、不付款、不接受动作；由本体模拟时钟采样，不用协程等待。
 * 协作：绑定版本/控制代际变化时撤销旧手势，并等待真实松键；Tap与LongPress只会有一个胜者。
 */
using System;
namespace Ossastra.CharacterCore.Abilities
{
    public enum GestureMode { Press, Hold, Release, TapOrLongPress }
    public enum GestureSignal { None, Trigger, Begin, Tap, LongPress, End, Cancel }

    public sealed class GestureRecognizer
    {
        private readonly GestureMode mode;
        private readonly double threshold;
        private bool armed, down, longWon;
        private int epoch=int.MinValue;
        private double began;

        public GestureRecognizer(GestureMode mode,double threshold)
        {
            if(!Enum.IsDefined(typeof(GestureMode),mode)||!BasicAttackDefinition.Finite(threshold)||threshold<=0)
                throw new ArgumentException("Gesture mode/threshold invalid");
            this.mode=mode;this.threshold=threshold;
        }

        public GestureSignal Sample(bool held,double now,int generation)
        {
            if(!BasicAttackDefinition.Finite(now))throw new ArgumentException("Gesture time invalid");
            if(epoch!=generation)
            {
                var cancel=down;epoch=generation;armed=!held;down=false;longWon=false;
                return cancel?GestureSignal.Cancel:GestureSignal.None;
            }
            if(!held)
            {
                armed=true;if(!down)return GestureSignal.None;
                down=false;
                // 低帧率可能直接跨过阈值来到松键帧；按实际持续时间选胜者，不能误发短按。
                if(mode==GestureMode.TapOrLongPress)return longWon?GestureSignal.End:
                    now-began>=threshold?GestureSignal.LongPress:GestureSignal.Tap;
                return mode==GestureMode.Release?GestureSignal.Trigger:GestureSignal.End;
            }
            if(!armed)return GestureSignal.None;
            if(!down)
            {
                down=true;began=now;longWon=false;
                return mode==GestureMode.Press?GestureSignal.Trigger:mode==GestureMode.Hold?GestureSignal.Begin:GestureSignal.None;
            }
            if(mode==GestureMode.TapOrLongPress&&!longWon&&now-began>=threshold)
            {longWon=true;return GestureSignal.LongPress;}
            return GestureSignal.None;
        }
    }
}
