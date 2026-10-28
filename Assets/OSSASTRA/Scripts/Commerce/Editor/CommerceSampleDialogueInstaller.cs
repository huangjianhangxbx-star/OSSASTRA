/* 脚本：CommerceSampleDialogueInstaller
 * 功能：一次性将固定原型SAMPLE对话迁为可编辑SO作者数据；已存在资产只读返回，不覆盖用户配置。
 * 来源：原型bb969cc的game/src/core/dialogue/content.ts；三句、A/B标记/提示/末句和重访分支完整保留。
 * 边界：仅Editor资产工厂；生产会话机不包含这些台词或路由，不接任务、好感或声音。
 */
using System;
using UnityEngine;
using UnityEditor;
using Ossastra.Commerce.Runtime;
namespace Ossastra.Commerce.Editor
{
    public static class CommerceSampleDialogueInstaller
    {
        public static CommerceDialogueSO EnsureSampleAsset(string path)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("对话作者资产只在编辑模式创建");
            if(string.IsNullOrWhiteSpace(path))throw new ArgumentException("对话资产路径缺失");
            path=path.Replace('\\','/');
            if(!path.StartsWith("Assets/",StringComparison.Ordinal)||!path.EndsWith(".asset",StringComparison.OrdinalIgnoreCase)||path.Split('/').AnySegmentInvalid())throw new ArgumentException("对话资产路径必须位于Assets中且不包含相对跳转");
            var existing=AssetDatabase.LoadMainAssetAtPath(path);
            if(existing!=null)
            {
                var dialogue=existing as CommerceDialogueSO;
                if(dialogue==null)throw new ArgumentException("目标路径已有其他类型资产，未覆盖："+path);
                return dialogue;
            }
            var project=System.IO.Directory.GetParent(Application.dataPath).FullName;
            if(!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path))||System.IO.File.Exists(System.IO.Path.Combine(project,path)))
                throw new ArgumentException("目标已有无法加载的资产文件，未覆盖："+path);
            string folder=path.Substring(0,path.LastIndexOf('/'));EnsureFolder(folder);
            var asset=ScriptableObject.CreateInstance<CommerceDialogueSO>();asset.definitionId="companion";
            asset.scripts=new[]{
                new CommerceDialogueScriptConfig
                {
                    id="sample.companion.default",entryNodeId="greeting",priority=0,
                    nodes=new[]{
                        Line("greeting","我在这里，准备好继续探索了吗？","team"),
                        Line("team","前面的路还不熟悉，我们一起留意周围吧。","plan"),
                        Line("plan","出发前，我想听听你的打算。","choice"),
                        new CommerceDialogueNodeConfig{id="choice",kind=CommerceDialogueNodeKind.Choice,text="接下来，要先检查补给还是观察前路？",choices=new[]{
                            new CommerceDialogueChoiceConfig{id="A",text="检查补给",next="flagA"},
                            new CommerceDialogueChoiceConfig{id="B",text="观察前路",next="flagB"}}},
                        Flag("flagA","sample.dialogue.choiceA","noticeA"),
                        Notice("noticeA","SAMPLE · 已记录检查补给的选择","endA"),
                        Line("endA","好，先检查补给，我会留意附近的动静。"),
                        Flag("flagB","sample.dialogue.choiceB","noticeB"),
                        Notice("noticeB","SAMPLE · 已记录观察前路的选择","endB"),
                        Line("endB","那就先看看前路，我们保持彼此照应。")
                    }
                },
                FollowUp("A","你刚才说要检查补给，我记住了。"),
                FollowUp("B","你刚才说要观察前路，我记住了。")
            };
            // 创建前走同一纯图校验；只保存本次新资产，不保存其他用户dirty资产。
            try{asset.Freeze();AssetDatabase.CreateAsset(asset,path);AssetDatabase.SaveAssetIfDirty(asset);return asset;}
            catch{if(!AssetDatabase.Contains(asset))UnityEngine.Object.DestroyImmediate(asset);throw;}
        }
        private static CommerceDialogueNodeConfig Line(string id,string text,string next=null)
            =>new CommerceDialogueNodeConfig{id=id,kind=CommerceDialogueNodeKind.Line,text=text,next=next};
        private static CommerceDialogueNodeConfig Flag(string id,string key,string next)
            =>new CommerceDialogueNodeConfig{id=id,kind=CommerceDialogueNodeKind.SetFlag,flagKey=key,flagValue=true,next=next};
        private static CommerceDialogueNodeConfig Notice(string id,string text,string next)
            =>new CommerceDialogueNodeConfig{id=id,kind=CommerceDialogueNodeKind.Notice,text=text,next=next};
        private static CommerceDialogueScriptConfig FollowUp(string branch,string first)
            =>new CommerceDialogueScriptConfig{id="sample.companion.after"+branch,entryNodeId="first",priority=10,
                conditions=new[]{new CommerceDialogueConditionConfig{flag="sample.dialogue.choice"+branch,expected=true}},
                nodes=new[]{Line("first",first,"last"),Line("last","准备好了就继续走吧，我会跟着你。")}};
        private static void EnsureFolder(string path)
        {
            string current="Assets";var parts=path.Split('/');
            for(int index=1;index<parts.Length;index++)
            {string next=current+"/"+parts[index];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[index]);current=next;}
        }
        private static bool AnySegmentInvalid(this string[] parts)
        {foreach(var part in parts)if(string.IsNullOrWhiteSpace(part)||part=="."||part=="..")return true;return false;}
    }
}
