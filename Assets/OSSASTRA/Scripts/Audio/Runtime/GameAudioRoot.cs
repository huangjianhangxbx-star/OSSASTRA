/* 脚本：GameAudioRoot
 * 功能：正式BGM唯一生命周期根，组织仲裁、混音、DSP传输与可观察记录。
 * 边界：不拥有角色/战斗状态，不播放战斗SFX；不修改全局AudioListener或工程音频设置。
 * 协作：场景Host发布只读快照；同内容先于hold短路；加载和UI监控没有独立播放权。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
namespace Ossastra.Audio
{
    [DefaultExecutionOrder(-1000),DisallowMultipleComponent]
    public sealed class GameAudioRoot:MonoBehaviour
    {
        [SerializeField] private BgmRuleSetSO rules;
        [SerializeField] private BgmMixProfileSO mixProfile;
        [SerializeField] private AudioMixerGroup output;
        [SerializeField] private bool persistAcrossScenes=true;
        private readonly Queue<string> history=new Queue<string>();
        private BgmContextHost host;
        private BgmContext context;
        private BgmContext? diagnosticContext;
        private bool contextValid,explicitStop;
        private double holdUntil;
        private int observedStarts;
        public static GameAudioRoot Instance {get;private set;}
        public bool IsOwner {get;private set;}
        public BgmTransport Transport {get;private set;}
        public BgmMixController Mix {get;private set;}
        public BgmChoice Choice {get;private set;}
        public BgmContext Context=>context;
        public bool ContextValid=>contextValid;
        public bool DiagnosticOverride=>diagnosticContext.HasValue;
        public string Status {get;private set;}="未初始化";
        public string[] History=>history.ToArray();
        public BgmRuleSetSO Rules=>rules;
        public double HoldUntil=>holdUntil;
        public bool IsBoundHost(BgmContextHost source)=>IsOwner&&host==source;
        private void Awake()
        {
            if(Instance!=null&&Instance!=this){Status="拒绝重复正式BGM所有者";enabled=false;Destroy(gameObject);return;}
            if(rules==null||mixProfile==null){Status="缺少音频规则/混音配置";enabled=false;return;}
            Instance=this;IsOwner=true;
            Mix=new BgmMixController(mixProfile.baselineGain,mixProfile.dialogueGain,mixProfile.duckSeconds);
            Mix.UserBgmVolume=PlayerPrefs.GetFloat("ossastra.audio.bgm",.24f);
            Mix.MasterVolume=PlayerPrefs.GetFloat("ossastra.audio.master",.75f);
            Mix.Muted=PlayerPrefs.GetInt("ossastra.audio.muted",0)!=0;
            Transport=new BgmTransport(gameObject,output,mixProfile.crossfadeSeconds);
            if(persistAcrossScenes)DontDestroyOnLoad(gameObject);
            Status="等待场景有效快照";
        }
        public void BindHost(BgmContextHost source)
        {
            if(!IsOwner||source==null)return;
            if(host!=source){if(host!=null)host.ReleaseWorldScope();diagnosticContext=null;Transport.InvalidateTarget(AudioSettings.dspTime);host=source;contextValid=false;Record("绑定新场景Host");}
        }
        public void Publish(BgmContextHost source,BgmContext value)
        {if(!IsOwner||host!=source)return;context=value;contextValid=true;}
        public void UnbindHost(BgmContextHost source)
        {
            if(host!=source)return;host=null;contextValid=false;diagnosticContext=null;
            // 与原型setWorld一致：旧歌可继续，晚到加载不能在失效世界抢播放权。
            Transport?.InvalidateTarget(AudioSettings.dspTime);Record("场景目标失效：保留已开始的声音");
        }
        private void LateUpdate()
        {
            if(!IsOwner||Transport==null)return;
            double now=AudioSettings.dspTime;
            if((contextValid||diagnosticContext.HasValue)&&!explicitStop)
            {
                var next=BgmArbitrator.Choose(diagnosticContext??context,rules);
                if(next!=null)
                {
                    bool same=Transport.CurrentKey==next.Track.PlaybackKey||Choice?.Track.PlaybackKey==next.Track.PlaybackKey;
                    bool title=next.Condition==BgmRuleCondition.Title||Choice?.Condition==BgmRuleCondition.Title;
                    if(same||next.Priority>=60||title||now>=holdUntil||Choice==null)
                    {
                        if(Choice==null||Choice.RuleId!=next.RuleId||Choice.Track!=next.Track)Record("规则 "+next.RuleId+"："+next.Reason);
                        Choice=next;
                        Transport.Request(next.Track,now);Transport.SetIntensity(next.Intensity,now);
                    }
                }
                else Status="无合法音乐配置";
            }
            Mix.Tick(Time.unscaledDeltaTime);
            Transport.Tick(now,Time.unscaledDeltaTime,Mix.CurrentGain);
            // 加载可用性在Tick中变化；hold只能从真实起播epoch计算，不能从请求帧推算。
            if(Transport.StartCount!=observedStarts)
            {observedStarts=Transport.StartCount;var track=Transport.CurrentTrack;double bar=track!=null&&track.knownBpm>0?60d/track.knownBpm*track.beatsPerBar:0;holdUntil=Transport.Epoch+(bar>0?bar*2:1);}
            Status=explicitStop?"显式停止，需显式Resume":!contextValid&&!diagnosticContext.HasValue?"世界目标失效 / "+Transport.Status:Transport.Status;
        }
        public IDisposable AcquireDialogueFocus()
        {if(!IsOwner||Mix==null)throw new InvalidOperationException("没有正式BGM所有者");return Mix.AcquireDialogueFocus();}
        public void StopMusic(){if(!IsOwner)return;explicitStop=true;Transport.Stop();Record("显式停止");}
        public void ResumeMusic(){if(!IsOwner)return;explicitStop=false;Transport.Resume();holdUntil=0;Record("显式恢复");}
        public void SetDiagnosticContext(BgmContext? value)
        {if(!IsOwner)return;diagnosticContext=value;holdUntil=0;if(value.HasValue)ResumeMusic();Record(value.HasValue?"DEVELOPMENT ONLY 覆盖":"撤销开发覆盖");}
        public void SaveUserSettings()
        {
            if(Mix==null)return;
            PlayerPrefs.SetFloat("ossastra.audio.bgm",Mix.UserBgmVolume);PlayerPrefs.SetFloat("ossastra.audio.master",Mix.MasterVolume);
            PlayerPrefs.SetInt("ossastra.audio.muted",Mix.Muted?1:0);PlayerPrefs.Save();
        }
        private void Record(string message)
        {if(history.Count>=64)history.Dequeue();history.Enqueue(AudioSettings.dspTime.ToString("F3")+" "+message);}
        private void OnApplicationFocus(bool focused){if(Mix!=null)Mix.Focused=focused;}
        private void OnDisable(){if(IsOwner){explicitStop=true;Transport?.Stop();Mix?.ClearSnapshots();}}
        private void OnDestroy()
        {if(!IsOwner)return;Transport?.Dispose();Mix?.ClearSnapshots();if(Instance==this)Instance=null;IsOwner=false;}
    }
}
