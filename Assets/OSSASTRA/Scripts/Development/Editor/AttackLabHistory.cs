// 用同一批实际记录绘制战斗与招式效果时间线、选中事件和导出文件，不在窗口重新执行判定。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ossastra.Development.Editor
{
    internal sealed class AttackLabHistory
    {
        [Serializable] internal sealed class TrialHeader
        {
            public int trial;
            public string actor, weapon, skill, version, conditions, parameters, scene;
        }
        [Serializable] private sealed class Export
        {
            public TrialHeader trial;
            public string action, issue;
            public int dropped;
            public AttackLabEvent[] events;
        }
        private static readonly string[] Tracks = {"输入","状态","动画","判定","反馈","招式效果"};
        private static readonly Color[] Colors = { new Color(.3f,.78f,.9f), new Color(.98f,.7f,.25f),
            new Color(.76f,.53f,.91f), new Color(.94f,.4f,.37f), new Color(.4f,.81f,.54f), new Color(.95f,.7f,.45f) };
        private readonly Dictionary<int, TrialHeader> headers = new Dictionary<int, TrialHeader>();
        private int chosenTrial = -1, chosenEvent = -1;
        private string action = "", issue = "", lastExport;
        private Vector2 eventsScroll;

        public void Remember(AttackLabSession session, string conditions, string parameters)
        {
            if (headers.ContainsKey(session.Generation)) return;
            if (headers.Count >= 100) headers.Remove(headers.Keys.Min());
            headers[session.Generation] = new TrialHeader { trial = session.Generation, actor = session.Actor.ActorId,
                weapon = session.WeaponLabel, skill = session.SkillLabel, version = session.Version,
                conditions = conditions, parameters = parameters, scene = session.gameObject.scene.path };
        }

        public void Draw(AttackLabTrace trace, int current)
        {
            var generations = trace.Events.Select(e => e.trial).Append(current).Distinct().OrderByDescending(i => i).ToArray();
            if (!generations.Contains(chosenTrial)) chosenTrial = -1;
            var labels = new[] {"跟随当前试验"}.Concat(generations.Select(i => headers.TryGetValue(i, out var info) ?
                $"试验 {i} · {info.version}" : $"试验 {i}")).ToArray();
            int index = chosenTrial < 0 ? 0 : Array.IndexOf(generations, chosenTrial) + 1;
            int next = EditorGUILayout.Popup("查看试验", index, labels);
            if (index != next) { chosenTrial = next == 0 ? -1 : generations[next - 1]; chosenEvent = -1; action = ""; }
            int trial = chosenTrial < 0 ? current : chosenTrial;
            var all = trace.Events.Where(e => e.trial == trial).ToArray();
            var actions = all.Where(e => !string.IsNullOrEmpty(e.action)).GroupBy(e => e.action).Reverse().ToArray();
            var actionLabels = new[] {"整次试验（含未关联输入）"}.Concat(actions.Select(g =>
                $"第 {g.First().stage} 段 · {g.First().label} · {g.Key.Substring(0, Math.Min(8, g.Key.Length))}")).ToArray();
            int ai = Array.FindIndex(actions, g => g.Key == action) + 1;
            int an = EditorGUILayout.Popup("查看动作", ai, actionLabels);
            string selected = an > 0 ? actions[an-1].Key : "";
            if (selected != action) { action = selected; chosenEvent = -1; }
            trace.SetObservedAttack(action);
            var rows = string.IsNullOrEmpty(action) ? all : all.Where(e => e.action == action || e.parent == action).ToArray();
            if (headers.TryGetValue(trial, out var selectedHeader))
            {
                var other = headers.Values.Where(h => h.trial < trial && h.conditions == selectedHeader.conditions && h.actor == selectedHeader.actor && h.version != selectedHeader.version)
                    .OrderByDescending(h => h.trial).FirstOrDefault();
                if (other != null)
                {
                    var oldRows = trace.Events.Where(e => e.trial == other.trial).ToArray();
                    EditorGUILayout.LabelField("同条件实测 · 整次试验", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(CompareLine(other, oldRows), EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField(CompareLine(selectedHeader, all), EditorStyles.wordWrappedMiniLabel);
                }
            }
            DrawTimeline(rows);
            if (rows.Length == 0) EditorGUILayout.LabelField("等待本次试验的正式事件。", EditorStyles.wordWrappedMiniLabel);
            else
            {
                int detail = chosenEvent < 0 || chosenEvent >= rows.Length ? rows.Length-1 : chosenEvent;
                var item = rows[detail];
                EditorGUILayout.LabelField(item.track+"  "+item.label+"   ·   渲染帧 "+item.frame+"   ·   物理步 "+item.physicsStep, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(item.detail ?? "", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField($"实时时钟 {item.realMs:F1} ms    模拟时钟 {item.simMs:F1} ms",EditorStyles.miniLabel);
                eventsScroll = EditorGUILayout.BeginScrollView(eventsScroll, GUILayout.Height(108));
                int start = Mathf.Clamp(Mathf.FloorToInt(eventsScroll.y / 20), 0, rows.Length - 1);
                GUILayout.Space(start * 20);
                int end = Math.Min(start+8,rows.Length);
                for (int i=start;i<end;i++)
                {
                    int row = rows.Length-1-i;
                    if (GUILayout.Button($"{rows[row].realMs:F0} ms   {rows[row].track}   {rows[row].label}",EditorStyles.miniButton,GUILayout.Height(18)))
                        chosenEvent = row;
                }
                GUILayout.Space((rows.Length-end)*20);
                EditorGUILayout.EndScrollView();
            }
            if (trace.Dropped > 0) EditorGUILayout.LabelField("较早记录已滚出缓冲池 · "+trace.Dropped,EditorStyles.miniLabel);
            issue = EditorGUILayout.TextField("问题备注", issue);
            using (new EditorGUI.DisabledScope(rows.Length == 0))
                if (GUILayout.Button("导出所看记录（Markdown + JSON）"))
                    lastExport = ExportRows(trial, action, rows, issue, trace.Dropped);
            if (!string.IsNullOrEmpty(lastExport) && GUILayout.Button("在文件夹中查看诊断记录"))
                EditorUtility.RevealInFinder(lastExport);
        }

        private static string CompareLine(TrialHeader header, AttackLabEvent[] events)
        {
            var queries = events.Where(e => !string.IsNullOrEmpty(e.queryType)).ToArray();
            var first = queries.FirstOrDefault();
            var damage = events.Where(e => !string.IsNullOrEmpty(e.outcome)).Sum(e => Math.Max(0, e.hpBefore - e.hpAfter));
            var shape = first == null ? "暂无查询" : $"{first.queryType}，首次半径 {first.radius:0.###}";
            return $"#{header.trial} {header.version} · {shape} · 查询 {queries.Length} 次 · 记录扣血 {damage}";
        }

        private void DrawTimeline(AttackLabEvent[] rows)
        {
            const float labelWidth = 60, rowHeight=19;
            var rect=GUILayoutUtility.GetRect(100, Tracks.Length*rowHeight+17,GUILayout.ExpandWidth(true));
            var ev=Event.current;
            double low=rows.Length>0 ? rows.Min(e=>e.realMs) : 0;
            double high=rows.Length>0 ? rows.Max(e=>e.realMs) : low;
            float width=Mathf.Max(1,rect.width-labelWidth-9);
            int hit=-1; float nearest=8;
            for(int k=0;k<Tracks.Length;k++)
            {
                float y=rect.y+k*rowHeight;
                if(ev.type==EventType.Repaint)
                {
                    EditorGUI.DrawRect(new Rect(rect.x+labelWidth,y+3,width+8,rowHeight-4),
                        EditorGUIUtility.isProSkin ? new Color(.12f,.13f,.15f) : new Color(.78f,.8f,.82f));
                    GUI.Label(new Rect(rect.x,y,labelWidth,rowHeight),Tracks[k],EditorStyles.miniLabel);
                }
                for(int i=0;i<rows.Length;i++)
                {
                    if(rows[i].track!=Tracks[k]) continue;
                    float x=rect.x+labelWidth+3+(float)((rows[i].realMs-low)/Math.Max(1,high-low))*width;
                    if(ev.type==EventType.Repaint) EditorGUI.DrawRect(new Rect(x,y+5,i==chosenEvent?5:3,rowHeight-8),i==chosenEvent?Color.white:Colors[k]);
                    if(ev.type==EventType.MouseDown && ev.mousePosition.y>=y && ev.mousePosition.y<y+rowHeight)
                    {
                        float d=Mathf.Abs(ev.mousePosition.x-x);
                        if(d<nearest){nearest=d;hit=i;}
                    }
                }
            }
            if(ev.type==EventType.Repaint) GUI.Label(new Rect(rect.x+labelWidth,rect.y+rowHeight*Tracks.Length,rect.width-labelWidth,17),
                $"0 → {high-low:F0} ms    实际经过时间",EditorStyles.miniLabel);
            if(hit>=0) { chosenEvent=hit; ev.Use(); }
        }

        public string ExportRows(int trial,string id,AttackLabEvent[] rows,string description,int dropped)
        {
            if(!headers.TryGetValue(trial,out var header)) header=new TrialHeader{trial=trial,version="无快照"};
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../Reports/SideViewAttackLab"));
            Directory.CreateDirectory(dir);
            string file=Path.Combine(dir,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-T"+trial);
            string json=JsonUtility.ToJson(new Export{trial=header,action=id,issue=description,events=rows,dropped=dropped},true);
            var md=new StringBuilder("# 横版攻击诊断\n\n");
            md.AppendLine($"{header.actor} · {header.weapon} · {header.skill}").AppendLine()
                .AppendLine($"试验 {trial} · {header.version} · 场景 {header.scene}").AppendLine()
                .AppendLine("条件 "+header.conditions).AppendLine().AppendLine(header.parameters).AppendLine()
                .AppendLine("观察问题 "+description).AppendLine().AppendLine("动作 "+(string.IsNullOrEmpty(id)?"整次试验":id)).AppendLine();
            foreach(var group in Tracks)
            {
                var matches=rows.Where(r=>r.track==group).ToArray();
                md.AppendLine($"## {group}（{matches.Length}）").AppendLine();
                foreach(var row in matches.Take(12)) md.AppendLine($"- {row.realMs:F1} ms　{row.label}　{row.detail}");
                if(matches.Length==0) md.AppendLine("此次选区无记录。");
                md.AppendLine();
            }
            md.AppendLine("完整事件见同名 JSON。记录时间不包含设备到屏幕的端到端延迟。音画请求与最终呈现请分别核对，未采集环节无法推定。");
            File.WriteAllText(file+".json",json,new UTF8Encoding(false));
            File.WriteAllText(file+".md",md.ToString(),new UTF8Encoding(false));
            return file+".md";
        }
    }
}
