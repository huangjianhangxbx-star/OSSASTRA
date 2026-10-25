/* 脚本：GameAudioRoot
 * 功能：正式唯一音频生命周期根，组合专业BGM/SFX仲裁、共享混音与有限播放器。
 * 边界：不拥有角色/战斗状态，不修改全局AudioListener或工程音频设置。
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
        [SerializeField] private GameAudioBootstrapSO bootstrap;
        private readonly HashSet<SfxSourceHandle> sources=new HashSet<SfxSourceHandle>();
        private readonly Dictionary<int,CombatSfxFactAdapter> factOwners=new Dictionary<int,CombatSfxFactAdapter>();
        private readonly Queue<SfxObservation> sfxHistory=new Queue<SfxObservation>();
        private string sceneScope;
        public sealed class SfxObservation
        {
            public SfxRequest Request{get;internal set;}
            public SfxDecision Decision{get;internal set;}
            public bool SourcePlayIssued{get;internal set;}
            public string PlaybackReason{get;internal set;}
        }
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
        public SfxArbitrator Sfx{get;private set;}
        public SfxVoicePlayer SfxPlayer{get;private set;}
        public GameAudioMixService UnifiedMix{get;private set;}
        public string WorldScopeId=>Sfx?.WorldScopeId??"";
        public int SourceLeaseCount=>sources.Count;
        public bool SfxAvailable=>IsOwner&&isActiveAndEnabled&&SfxPlayer!=null&&UnifiedMix!=null&&UnifiedMix.IsConfigured&&!string.IsNullOrEmpty(WorldScopeId);
        public string SfxStatus{get;private set;}="未初始化";
        public SfxObservation[] SfxHistory=>sfxHistory.ToArray();
        public GameAudioBootstrapSO Bootstrap=>bootstrap;
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
            if(bootstrap==null)bootstrap=GameAudioBootstrap.LoadConfiguration();
            if(bootstrap!=null){if(rules==null)rules=bootstrap.rules;if(mixProfile==null)mixProfile=bootstrap.mixProfile;}
            if(rules==null||mixProfile==null){Status="缺少音频规则/混音配置";enabled=false;return;}
            Instance=this;IsOwner=true;
            Mix=new BgmMixController(mixProfile.baselineGain,mixProfile.dialogueGain,mixProfile.duckSeconds);
            Mix.UserBgmVolume=PlayerPrefs.GetFloat("ossastra.audio.bgm",.24f);
            Mix.MasterVolume=PlayerPrefs.GetFloat("ossastra.audio.master",.75f);
            Mix.Muted=PlayerPrefs.GetInt("ossastra.audio.muted",0)!=0;
            string reason="BOOTSTRAP_MISSING";
            if(bootstrap!=null&&bootstrap.Validate(out reason))
            {
                UnifiedMix=new GameAudioMixService(Mix,bootstrap.mixer);
                if(UnifiedMix.IsConfigured)
                {
                    output=bootstrap.bgmOutput;UnifiedMix.LoadUserSettings();
                    Sfx=new SfxArbitrator(bootstrap.softVoices,bootstrap.hardVoices);
                    SfxPlayer=new SfxVoicePlayer(gameObject,Sfx,bootstrap.Route,bootstrap.hardVoices);
                    BeginSfxWorld(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path+"#"+UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle);
                    SfxStatus="统一SFX就绪";
                }
                else SfxStatus=UnifiedMix.LastError;
            }
            else SfxStatus=reason;
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
            bool unified=UnifiedMix!=null&&UnifiedMix.IsConfigured;
            if(unified&&!UnifiedMix.Tick())SfxStatus=UnifiedMix.LastError;
            Transport.Tick(now,Time.unscaledDeltaTime,unified?Mix.LocalCurrentGain:Mix.CurrentGain);
            SfxPlayer?.Tick(now);
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
            PlayerPrefs.SetInt("ossastra.audio.muted",Mix.Muted?1:0);UnifiedMix?.SaveUserSettings();PlayerPrefs.Save();
        }
        public void BeginSfxWorld(string sceneIdentity)
        {
            if(Sfx==null||sceneScope==sceneIdentity&&!string.IsNullOrEmpty(Sfx.WorldScopeId))return;
            foreach(var source in new List<SfxSourceHandle>(sources))source.Dispose();
            SfxPlayer.StopWorld();sceneScope=sceneIdentity;Sfx.SetWorldScope(sceneIdentity+":"+Guid.NewGuid().ToString("N"));
        }
        public SfxSourceHandle RegisterSfxSource(string sourceId)
        {
            if(!SfxAvailable||string.IsNullOrWhiteSpace(sourceId))return null;
            var handle=new SfxSourceHandle(this,sourceId,WorldScopeId,Sfx.OpenSource(sourceId));sources.Add(handle);return handle;
        }
        internal bool TryClaimSfxFacts(CombatSfxFactAdapter candidate)
        {
            if(candidate==null||!SfxAvailable)return false;
            int scene=candidate.gameObject.scene.handle;
            if(factOwners.TryGetValue(scene,out var owner)&&owner!=null&&owner!=candidate&&owner.isActiveAndEnabled)return false;
            factOwners[scene]=candidate;return true;
        }
        internal void ReleaseSfxFacts(CombatSfxFactAdapter candidate)
        {if(candidate==null)return;int scene=candidate.gameObject.scene.handle;if(factOwners.TryGetValue(scene,out var owner)&&owner==candidate)factOwners.Remove(scene);}
        internal void ReleaseSfxSource(SfxSourceHandle source)
        {if(!sources.Remove(source))return;SfxPlayer?.StopSource(source.SourceId,source.Generation);Sfx?.CloseSource(source.SourceId,source.Generation);}
        public SfxDecision SubmitSfx(SfxRequest request)
        {
            SfxDecision decision;bool issued=false;string reason="";
            if(!SfxAvailable)decision=new SfxDecision(false,"OWNER_UNAVAILABLE:"+SfxStatus);
            else if(!bootstrap.sfxCatalog.TryResolve(request.EventId,out var definition,out reason))decision=new SfxDecision(false,reason);
            else
            {
                decision=Sfx.Submit(request,definition,Time.realtimeSinceStartupAsDouble);
                if(decision.Accepted){issued=SfxPlayer.TryPlay(decision);if(!issued)reason=SfxPlayer.LastError;}
            }
            if(sfxHistory.Count>=128)sfxHistory.Dequeue();
            sfxHistory.Enqueue(new SfxObservation{Request=request,Decision=decision,SourcePlayIssued=issued,PlaybackReason=issued?"PLAY_ISSUED":string.IsNullOrEmpty(reason)?decision.Reason:reason});
            return decision;
        }
        private void Record(string message)
        {if(history.Count>=64)history.Dequeue();history.Enqueue(AudioSettings.dspTime.ToString("F3")+" "+message);}
        private void OnApplicationFocus(bool focused){if(Mix!=null)Mix.Focused=focused;}
        private void OnEnable()
        {if(IsOwner&&Sfx!=null&&string.IsNullOrEmpty(Sfx.WorldScopeId)){var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();BeginSfxWorld(scene.path+"#"+scene.handle);}}
        private void OnDisable(){if(IsOwner){explicitStop=true;Transport?.Stop();Mix?.ClearSnapshots();foreach(var source in new List<SfxSourceHandle>(sources))source.Dispose();SfxPlayer?.StopWorld();sceneScope=null;}}
        private void OnDestroy()
        {if(!IsOwner)return;SfxPlayer?.Dispose();Transport?.Dispose();Mix?.ClearSnapshots();if(Instance==this)Instance=null;IsOwner=false;}
    }
}
