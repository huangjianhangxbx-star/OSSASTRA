/*
 * 脚本：RespawnSchedule
 * 功能：场景队伍返场的模拟时间和待处理事务，不复制HP或Current。
 * 边界：消费真实存活/可用快照；落点失败不完成，恢复成功才递增代际。
 */
using System;
using System.Collections.Generic;
namespace Ossastra.CharacterCore
{
    public sealed class RespawnSchedule
    {
        private readonly double singleDelay, wipeDelay;
        private readonly double[] due, diedAt;
        private readonly bool[] pending;
        private readonly int[] generations;
        private double wipeDue;
        private int[] ready = Array.Empty<int>();
        public double Time { get; private set; }
        public bool IsWipe { get; private set; }
        public int[] ReadyIndices => (int[])ready.Clone();
        public int Generation(int index) => generations[index];
        public double DiedAt(int index) => diedAt[index];
        public double DueAt(int index) => pending[index] ? (IsWipe ? wipeDue : due[index]) : -1;
        public RespawnSchedule(int count, double singleDelay, double wipeDelay)
        {
            if (count < 1 || !Valid(singleDelay) || !Valid(wipeDelay)) throw new ArgumentOutOfRangeException();
            this.singleDelay=singleDelay; this.wipeDelay=wipeDelay;
            due=new double[count]; diedAt=new double[count]; pending=new bool[count]; generations=new int[count];
            for(var i=0;i<count;i++) diedAt[i]=-1;
        }
        // 原死亡事件只登记本场模拟时刻；HP仍由调用方的本体Health决定。
        public void RecordDeath(int index)
        {
            if(pending[index]) return;
            pending[index]=true; diedAt[index]=Time; due[index]=Time+singleDelay;
        }
        public void Tick(bool[] alive, bool[] available, double delta, bool suspended)
        {
            if (alive.Length!=pending.Length || available.Length!=pending.Length) throw new ArgumentException("Party size changed");
            ready=Array.Empty<int>();
            if (suspended || !Valid(delta)) return;
            var allDead=true;
            for(var i=0;i<pending.Length;i++)
            {
                if(alive[i] || !available[i]) { pending[i]=false; allDead=false; }
                else if(!pending[i]) RecordDeath(i);
            }
            if(allDead && !IsWipe) wipeDue=Time+wipeDelay;
            IsWipe=allDead;
            Time+=delta;
            var list=new List<int>();
            for(var i=0;i<pending.Length;i++)
                if(pending[i] && Time+1e-9 >= (IsWipe?wipeDue:due[i])) list.Add(i);
            ready=list.ToArray();
        }
        public void Complete(int index)
        {
            if(!pending[index] || Array.IndexOf(ready,index)<0) throw new InvalidOperationException("Not due");
            pending[index]=false; generations[index]++;
        }
        private static bool Valid(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value>=0;
    }
}
