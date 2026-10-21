/*
 * 脚本：BasicChainState
 * 功能：配置段数、可选命中要求的手动普攻接续资格和最多一条预输入。
 * 边界：不累计delta，不产生动作/伤害；绝对时间只读原BasicActionRuntime模拟时钟。
 */
using System;
namespace Ossastra.CharacterCore
{
    public sealed class BasicChainState
    {
        private readonly double preInput, bufferLifetime, continuationWindow;
        private readonly int stageCount;
        private readonly bool requireHit;
        private Guid actionId;
        private int stage;
        private bool hit, buffered;
        private double readyAt, bufferUntil;
        public bool HasBuffer => buffered;
        private bool CanContinue => stage>0&&stage<stageCount&&(!requireHit||hit);
        public double WindowUntil => CanContinue?readyAt+continuationWindow:-1;
        public BasicChainState(double preInput,double bufferLifetime,double continuationWindow,int stageCount=2)
            :this(preInput,bufferLifetime,continuationWindow,stageCount,true) { }
        public BasicChainState(double preInput,double bufferLifetime,double continuationWindow,int stageCount,bool requireHit)
        {
            if(!Valid(preInput)||!Valid(bufferLifetime)||!Valid(continuationWindow))throw new ArgumentOutOfRangeException();
            if(stageCount<1)throw new ArgumentOutOfRangeException(nameof(stageCount));
            this.stageCount=stageCount;this.requireHit=requireHit;this.preInput=preInput;this.bufferLifetime=bufferLifetime;this.continuationWindow=continuationWindow;
        }
        public int Request(double now,bool attackReady,bool eligible)
        {
            if(!eligible){Clear();return 0;}
            if(attackReady) {buffered=false;return CanContinue&&now<=WindowUntil?stage+1:1;}
            if(CanContinue&&!buffered&&now>=readyAt-preInput&&now<=readyAt)
            {buffered=true;bufferUntil=now+bufferLifetime;}
            return 0;
        }
        public int Poll(double now,bool attackReady)
        {
            if(!buffered)return 0;
            if(now>bufferUntil||now>WindowUntil){buffered=false;return 0;}
            if(!attackReady)return 0;
            buffered=false;return CanContinue?stage+1:0;
        }
        public void Accepted(int stage,Guid id,double now,double attackReadyAt)
        {this.stage=stage;actionId=id;readyAt=now+attackReadyAt;hit=false;buffered=false;}
        public void Hit(Guid id,bool applied){if(actionId==id)hit=applied;}
        public void Clear(){stage=0;actionId=Guid.Empty;hit=false;buffered=false;}
        private static bool Valid(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v)&&v>=0;
    }
}
