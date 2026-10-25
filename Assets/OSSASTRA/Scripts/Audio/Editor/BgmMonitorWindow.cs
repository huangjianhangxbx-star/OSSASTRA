/* 脚本：BgmMonitorWindow
 * 功能：观察唯一真实BGM根，提供明确标记的开发覆盖、音量与对话令牌。
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
        [MenuItem("XingHai/OSSASTRA/Audio/BGM Monitor")]
        public static void Open()=>GetWindow<BgmMonitorWindow>("BGM Monitor");
        private void OnInspectorUpdate()=>Repaint();
        private void OnGUI()
        {
            var root=GameAudioRoot.Instance;
            if(root==null){EditorGUILayout.HelpBox("进入 SC_BGM_AudioMigration 的 Play 后观察真实音频根。此窗口不创建播放器。",MessageType.Info);return;}
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
            EditorGUILayout.BeginHorizontal();if(GUILayout.Button("显式Stop"))root.StopMusic();if(GUILayout.Button("显式Resume"))root.ResumeMusic();EditorGUILayout.EndHorizontal();
            foreach(var line in root.History)EditorGUILayout.LabelField(line,EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }
        private void Release(){dialogue?.Dispose();dialogue=null;if(ownsOverride&&controlled!=null)controlled.SetDiagnosticContext(null);ownsOverride=false;}
        private void OnDisable()=>Release();
    }
}
