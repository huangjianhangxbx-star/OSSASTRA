/*
 * 脚本：BasicChainState
 * 功能：两段手动普攻的接续资格和最多一条预输入。
 * 边界：不累计delta，不产生动作/伤害；绝对时间只读原BasicActionRuntime模拟时钟。
 */
using System;
namespace Ossastra.CharacterCore
{
    public sealed class BasicChainState
    {
        private readonly double preInput, bufferLifetime, continuationWindow;
        private Guid actionId;
        private int stage;
        private bool hit, buffered;
        private double readyAt, bufferUntil;
        public bool HasBuffer => buffered;
        public double WindowUntil => stage==1&&hit?readyAt+continuationWindow:-1;
        public BasicChainState(double preInput,double bufferLifetime,double continuationWindow)
        {
            if(!Valid(preInput)||!Valid(bufferLifetime)||!Valid(continuationWindow))throw new ArgumentOutOfRangeException();
            this.preInput=preInput;this.bufferLifetime=bufferLifetime;this.continuationWindow=continuationWindow;
        }
        public int Request(double now,bool attackReady,bool eligible)
        {
            if(!eligible){Clear();return 0;}
            if(attackReady) {buffered=false;return stage==1&&hit&&now<=WindowUntil?2:1;}
            if(stage==1&&hit&&!buffered&&now>=readyAt-preInput&&now<=readyAt)
            {buffered=true;bufferUntil=now+bufferLifetime;}
            return 0;
        }
        public int Poll(double now,bool attackReady)
        {
            if(!buffered)return 0;
            if(now>bufferUntil||now>WindowUntil){buffered=false;return 0;}
            if(!attackReady)return 0;
            buffered=false;return stage==1&&hit?2:0;
        }
        public void Accepted(int stage,Guid id,double now,double attackReadyAt)
        {this.stage=stage;actionId=id;readyAt=now+attackReadyAt;hit=false;buffered=false;}
        public void Hit(Guid id,bool applied){if(actionId==id)hit=applied;}
        public void Clear(){stage=0;actionId=Guid.Empty;hit=false;buffered=false;}
        private static bool Valid(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v)&&v>=0;
    }
}
