/* 脚本：BgmMonitorWindow
 * 功能：观察唯一真实音频根，保留BGM传输信息并展示SFX准入、路由、池与租约。
 * 边界：不创建播放器，不写战斗会话；关闭只撤自己持有的覆盖与混音令牌。
 */
using System;
using UnityEditor;
using UnityEngine;
namespace Ossastra.Audio.Editor
{
    public sealed class BgmMonitorWindow:EditorWindow
    {
        private IDisposable dialogue;
        private GameAudioRoot controlled;
        private Vector2 scroll;
        private bool ownsOverride;
        private SfxSourceHandle audition;
        private int auditionSequence;
        private int auditionEvent;
        [MenuItem("XingHai/OSSASTRA/Audio/BGM Monitor")]
        public static void Open()=>GetWindow<BgmMonitorWindow>("BGM Monitor");
        [MenuItem("XingHai/OSSASTRA/Audio/Unified Audio Monitor")]
        public static void OpenUnified()=>GetWindow<BgmMonitorWindow>("Audio Monitor");
        private void OnInspectorUpdate()=>Repaint();
        private void OnGUI()
        {
            var root=GameAudioRoot.Instance;
            if(root==null){EditorGUILayout.HelpBox("进入 SC_AUDIO_M02_SoundIntegration 的 Play 后观察统一音频根。此窗口不创建播放器。",MessageType.Info);return;}
            if(controlled!=root){Release();controlled=root;}
            var t=root.Transport;scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("真实BGM所有者",EditorStyles.boldLabel);EditorGUILayout.ObjectField(root,typeof(GameAudioRoot),true);
            EditorGUILayout.LabelField("状态",root.Status);EditorGUILayout.LabelField("世界快照",JsonUtility.ToJson(root.Context));
            EditorGUILayout.LabelField("规则",root.Choice?.RuleId??"无");EditorGUILayout.LabelField("原因",root.Choice?.Reason??"无");
            EditorGUILayout.SelectableLabel("PlaybackKey: "+t.CurrentKey,EditorStyles.textArea,GUILayout.Height(52));
            EditorGUILayout.LabelField("Backend / Transport",t.BackendId+" / "+t.TransportId);
            EditorGUILayout.LabelField("组起播 / PlayScheduled / 分配音源",t.StartCount+" / "+t.SourceStartCount+" / "+t.SourceCount);
            EditorGUILayout.LabelField("DSP epoch / elapsed",t.Epoch.ToString("F4")+" / "+t.Elapsed.ToString("F3"));
            EditorGUILayout.LabelField("当前内容",t.CurrentTrack==null?"无":t.CurrentTrack.contentId+" / "+t.CurrentTrack.contentVersion+" / "+t.CurrentTrack.sectionId);
            EditorGUILayout.LabelField("Pending / Fading",t.PendingKey+" / "+t.FadingKey);
            EditorGUILayout.LabelField("真实预约 / DSP起点",t.ScheduledKey+" / "+t.ScheduledEpoch.ToString("F4"));
            EditorGUILayout.LabelField("循环起止 / hold截止",(t.CurrentTrack==null?"无":t.CurrentTrack.loopStartSeconds+" / "+t.CurrentTrack.loopEndSeconds)+" / "+root.HoldUntil.ToString("F4"));
            EditorGUILayout.LabelField("Bank / Layer gains",string.Join(", ",t.BankGains)+" / "+string.Join(", ",t.LayerGains));
            EditorGUILayout.LabelField("代次 / 停止 / 加载",t.Generation+" / "+t.StopGeneration+" / "+t.LoadingGeneration);
            EditorGUILayout.LabelField("Gain / Target / Tokens",root.Mix.CurrentGain.ToString("F5")+" / "+root.Mix.TargetGain.ToString("F5")+" / "+root.Mix.SnapshotCount);
            if(!string.IsNullOrEmpty(t.LastError))EditorGUILayout.HelpBox(t.LastError,MessageType.Warning);
            EditorGUILayout.Space();EditorGUILayout.LabelField("DEVELOPMENT ONLY：不写正式玩法状态",EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();foreach(string id in new[]{"title","explore","far","combat","boss"})if(GUILayout.Button(id))
            {root.SetDiagnosticContext(new BgmContext{title=id=="title",far=id=="far",engaged=id=="combat",boss=id=="boss"});ownsOverride=true;}EditorGUILayout.EndHorizontal();
            if(GUILayout.Button("撤销我的开发覆盖")){root.SetDiagnosticContext(null);ownsOverride=false;}
            if(GUILayout.Button(dialogue==null?"取得普通对话焦点（只duck）":"结束普通对话焦点"))
            {if(dialogue==null)dialogue=root.AcquireDialogueFocus();else{dialogue.Dispose();dialogue=null;}}
            EditorGUI.BeginChangeCheck();root.Mix.UserBgmVolume=EditorGUILayout.Slider("音乐用户音量",root.Mix.UserBgmVolume,0,1);root.Mix.MasterVolume=EditorGUILayout.Slider("用户主音量",root.Mix.MasterVolume,0,1);root.Mix.Muted=EditorGUILayout.Toggle("静音",root.Mix.Muted);if(EditorGUI.EndChangeCheck())root.SaveUserSettings();
            EditorGUILayout.Space();EditorGUILayout.LabelField("统一 SFX / Mixer",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("状态 / 世界",root.SfxStatus+" / "+root.WorldScopeId);
            EditorGUILayout.LabelField("当前声音内容",root.SfxContentStatus);
            foreach(var facts in UnityEngine.Object.FindObjectsOfType<CombatSfxFactAdapter>())
            {
                EditorGUILayout.ObjectField("战斗语义配置",facts.SoundProfile,typeof(CombatSoundProfileSO),false);
                EditorGUILayout.LabelField("留白事件 / 最近语义",facts.UnmappedFacts+" / "+facts.LastUnmapped);
            }
            EditorGUILayout.LabelField("来源租约 / 声部",root.SourceLeaseCount+" / "+(root.Sfx?.ActiveCount??0));
            EditorGUILayout.LabelField("音乐局部 / 完整增益",root.Mix.LocalCurrentGain.ToString("F5")+" / "+root.Mix.CurrentGain.ToString("F5"));
            if(root.UnifiedMix!=null)
            {
                EditorGUILayout.ObjectField("唯一共享Mixer",root.UnifiedMix.Mixer,typeof(UnityEngine.Audio.AudioMixer),false);
                EditorGUI.BeginChangeCheck();root.UnifiedMix.SfxVolume=EditorGUILayout.Slider("全部音效",root.UnifiedMix.SfxVolume,0,1);
                foreach(SfxBus bus in Enum.GetValues(typeof(SfxBus)))root.UnifiedMix.SetBusVolume(bus,EditorGUILayout.Slider(bus+" 用户组",root.UnifiedMix.GetBusVolume(bus),0,1));
                if(EditorGUI.EndChangeCheck())root.SaveUserSettings();
                if(!string.IsNullOrEmpty(root.UnifiedMix.LastError))EditorGUILayout.HelpBox(root.UnifiedMix.LastError,MessageType.Error);
            }
            if(root.SfxPlayer!=null)
            {
                var player=root.SfxPlayer;EditorGUILayout.LabelField("池 / 活跃 / 实际起播 / 回收",player.PoolSize+" / "+player.ActiveCount+" / "+player.StartedCount+" / "+player.ReturnedCount);
                if(!string.IsNullOrEmpty(player.LastError))EditorGUILayout.LabelField("最近播放拒绝",player.LastError);
                foreach(var voice in player.Voices)if(voice.VoiceId!=0&&root.Sfx.TryGetVoice(voice.VoiceId,out var record))
                    EditorGUILayout.LabelField("Voice "+voice.VoiceId,record.Request.EventId+" | "+record.Request.SourceId+" | "+(record.Definition==null?"定义已删除":record.Definition.bus.ToString())+" | priority "+record.Priority+" | "+record.Request.WorldPosition);
            }
            if(root.SfxCatalog!=null)
            {
                var events=root.SfxCatalog.events??Array.Empty<SfxEventDefinitionSO>();var names=Array.ConvertAll(events,e=>e==null?"MISSING":e.eventId);
                if(names.Length>0)
                {
                    auditionEvent=EditorGUILayout.Popup("DEVELOPMENT ONLY 事件",Mathf.Clamp(auditionEvent,0,names.Length-1),names);
                    if(GUILayout.Button("提交开发试听事实（不改变HP/动作）"))
                    {if(audition==null||audition.IsDisposed||audition.WorldScopeId!=root.WorldScopeId){audition?.Dispose();audition=root.RegisterSfxSource("DEVELOPMENT_ONLY:AudioMonitor");}audition?.Submit(names[auditionEvent],"audition:"+(++auditionSequence),Vector3.zero);}
                }
            }
            foreach(var observation in root.SfxHistory)
                EditorGUILayout.LabelField(observation.Request.EventId+" | "+observation.Request.SourceId+" | "+observation.Request.FactKey+" | "+observation.Decision.Reason+" | "+observation.PlaybackReason,EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.BeginHorizontal();if(GUILayout.Button("显式Stop"))root.StopMusic();if(GUILayout.Button("显式Resume"))root.ResumeMusic();EditorGUILayout.EndHorizontal();
            foreach(var line in root.History)EditorGUILayout.LabelField(line,EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }
        private void Release(){dialogue?.Dispose();dialogue=null;audition?.Dispose();audition=null;if(ownsOverride&&controlled!=null)controlled.SetDiagnosticContext(null);ownsOverride=false;}
        private void OnDisable()=>Release();
    }
}
