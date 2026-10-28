/* 脚本：CommerceDialogue
 * 功能：配置驱动的对话图、优先级选段及单会话推进；标记写回宿主共享集合，支持重访分支。
 * 负责：验证节点/路由/效果环，按行或选择推进，提交SetFlag并返回独立显示帧。
 * 不负责：具体剧情文本、任务/好感/传送、UI输入、文件保存；宿主收到标记变动后请求统一保存。
 */
using System;
using System.Collections.Generic;
using System.Linq;
namespace Ossastra.Commerce
{
    public enum CommerceDialogueNodeKind { Line,Choice,SetFlag,Notice,End }
    public sealed class CommerceDialogueCondition
    {
        public string Flag {get;}
        public bool Expected {get;}
        public CommerceDialogueCondition(string flag,bool expected=true){Flag=flag;Expected=expected;}
    }
    public sealed class CommerceDialogueChoice
    {
        public string Id {get;}
        public string Text {get;}
        public string Next {get;}
        public CommerceDialogueChoice(string id,string text,string next){Id=id;Text=text;Next=next;}
    }
    public sealed class CommerceDialogueNode
    {
        private readonly CommerceDialogueChoice[] choices;
        public string Id {get;}
        public CommerceDialogueNodeKind Kind {get;}
        public string Text {get;}
        public string Next {get;}
        public string FlagKey {get;}
        public bool FlagValue {get;}
        public CommerceDialogueChoice[] Choices=>(CommerceDialogueChoice[])choices.Clone();
        public CommerceDialogueNode(string id,CommerceDialogueNodeKind kind,string text="",string next=null,CommerceDialogueChoice[] choices=null,string flagKey=null,bool flagValue=true)
        {Id=id;Kind=kind;Text=text??"";Next=next;this.choices=(CommerceDialogueChoice[])(choices??Array.Empty<CommerceDialogueChoice>()).Clone();FlagKey=flagKey;FlagValue=flagValue;}
    }
    public sealed class CommerceDialogueScript
    {
        private readonly CommerceDialogueNode[] nodes;
        private readonly CommerceDialogueCondition[] conditions;
        private readonly Dictionary<string,CommerceDialogueNode> lookup;
        public string Id {get;}
        public int Priority {get;}
        public string EntryNodeId {get;}
        public CommerceDialogueNode[] Nodes=>(CommerceDialogueNode[])nodes.Clone();
        public CommerceDialogueCondition[] Conditions=>(CommerceDialogueCondition[])conditions.Clone();
        public CommerceDialogueScript(string id,int priority,string entryNodeId,CommerceDialogueNode[] nodes,CommerceDialogueCondition[] conditions=null)
        {
            Id=id;Priority=priority;EntryNodeId=entryNodeId;this.nodes=(CommerceDialogueNode[])(nodes??Array.Empty<CommerceDialogueNode>()).Clone();
            this.conditions=(CommerceDialogueCondition[])(conditions??Array.Empty<CommerceDialogueCondition>()).Clone();
            if(string.IsNullOrWhiteSpace(id)||this.nodes.Any(n=>n==null||string.IsNullOrWhiteSpace(n.Id))||this.conditions.Any(c=>c==null||string.IsNullOrWhiteSpace(c.Flag)))throw new ArgumentException("对话段落、节点或条件缺失");
            lookup=this.nodes.ToDictionary(n=>n.Id,n=>n,StringComparer.Ordinal);
        }
        internal CommerceDialogueNode Node(string id)=>id!=null&&lookup.TryGetValue(id,out var node)?node:null;
        internal bool Eligible(ISet<string> flags)=>conditions.All(c=>flags.Contains(c.Flag)==c.Expected);
        internal void Validate()
        {
            if(Node(EntryNodeId)==null)throw new ArgumentException("对话入口节点缺失："+Id);
            foreach(var node in nodes)
            {
                if(!Enum.IsDefined(typeof(CommerceDialogueNodeKind),node.Kind))throw new ArgumentException("未知对话节点类型："+node.Id);
                if(!string.IsNullOrEmpty(node.Next)&&Node(node.Next)==null)throw new ArgumentException("对话路由目标缺失："+node.Id+" → "+node.Next);
                if(node.Kind==CommerceDialogueNodeKind.Line&&string.IsNullOrWhiteSpace(node.Text))throw new ArgumentException("对话台词为空："+node.Id);
                if(node.Kind==CommerceDialogueNodeKind.SetFlag&&string.IsNullOrWhiteSpace(node.FlagKey))throw new ArgumentException("对话标记键缺失："+node.Id);
                var choices=node.Choices;
                if(node.Kind==CommerceDialogueNodeKind.Choice)
                {
                    if(choices.Length==0||choices.Any(c=>c==null||string.IsNullOrWhiteSpace(c.Id)||string.IsNullOrWhiteSpace(c.Text)||Node(c.Next)==null)||choices.Select(c=>c.Id).Distinct().Count()!=choices.Length)throw new ArgumentException("对话选项或目标缺失："+node.Id);
                    if(!string.IsNullOrEmpty(node.Next))throw new ArgumentException("选择节点只能由选项路由："+node.Id);
                }
                else if(choices.Length!=0)throw new ArgumentException("非选择节点不应配置选项："+node.Id);
                if(node.Kind==CommerceDialogueNodeKind.End&&!string.IsNullOrEmpty(node.Next))throw new ArgumentException("结束节点不能继续路由："+node.Id);
                // 只禁止无显示等待的效果环；经过台词/选择的有意重复对话仍可由配置表达。
                var seen=new HashSet<string>(StringComparer.Ordinal);var effect=node;
                while(effect!=null&&(effect.Kind==CommerceDialogueNodeKind.SetFlag||effect.Kind==CommerceDialogueNodeKind.Notice))
                {if(!seen.Add(effect.Id))throw new ArgumentException("对话效果环："+effect.Id);effect=Node(effect.Next);}
            }
        }
    }
    public sealed class CommerceDialogueDefinition
    {
        private readonly CommerceDialogueScript[] scripts;
        public string Id {get;}
        public CommerceDialogueScript[] Scripts=>(CommerceDialogueScript[])scripts.Clone();
        public CommerceDialogueDefinition(string id,CommerceDialogueScript[] scripts)
        {
            if(string.IsNullOrWhiteSpace(id)||scripts==null||scripts.Length==0||scripts.Any(s=>s==null)||scripts.Select(s=>s.Id).Distinct().Count()!=scripts.Length)throw new ArgumentException("对话内容池或段落ID无效");
            Id=id;this.scripts=(CommerceDialogueScript[])scripts.Clone();foreach(var script in scripts)script.Validate();
        }
        // 沿固定原型selector：先优先级，再条件数，最后ID；不依赖作者数组偶然顺序。
        internal CommerceDialogueScript Select(ISet<string> flags)=>scripts.Where(s=>s.Eligible(flags)).OrderByDescending(s=>s.Priority).ThenByDescending(s=>s.Conditions.Length).ThenBy(s=>s.Id,StringComparer.Ordinal).FirstOrDefault();
    }
    public sealed class CommerceDialogueFrame
    {
        private readonly string[] choices,choiceIds,flags;
        public bool Ok {get;}
        public string Reason {get;}
        public bool Completed {get;}
        public string SpeakerKey {get;}
        public string DefinitionId {get;}
        public string NodeId {get;}
        public string Text {get;}
        public string[] Choices=>(string[])choices.Clone();
        public string[] ChoiceIds=>(string[])choiceIds.Clone();
        public bool HasNext {get;}
        public long Revision {get;}
        public string[] Flags=>(string[])flags.Clone();
        public string Notice {get;}
        internal CommerceDialogueFrame(bool ok,bool completed,string reason=null,string speakerKey=null,string definitionId=null,string nodeId=null,string text="",
            string[] choices=null,string[] choiceIds=null,bool hasNext=false,long revision=0,string[] flags=null,string notice="")
        {Ok=ok;Completed=completed;Reason=reason;SpeakerKey=speakerKey;DefinitionId=definitionId;NodeId=nodeId;Text=text;this.choices=(string[])(choices??Array.Empty<string>()).Clone();this.choiceIds=(string[])(choiceIds??Array.Empty<string>()).Clone();HasNext=hasNext;Revision=revision;this.flags=(string[])(flags??Array.Empty<string>()).Clone();Notice=notice;}
    }
    public sealed class CommerceDialogue
    {
        private readonly CommerceDialogueDefinition definition;
        private readonly ISet<string> flags;
        private readonly Action<string,bool> onFlagChanged;
        private CommerceDialogueScript script;
        private CommerceDialogueNode current;
        private string speaker,notice="";
        private long revision;
        private bool transitioning;
        public bool IsActive {get;private set;}
        public int NotificationErrorCount {get;private set;}
        public CommerceDialogue(CommerceDialogueDefinition definition,ISet<string> flags,Action<string,bool> onFlagChanged=null)
        {this.definition=definition??throw new ArgumentNullException(nameof(definition));this.flags=flags??throw new ArgumentNullException(nameof(flags));this.onFlagChanged=onFlagChanged;}
        public CommerceDialogueFrame Start(string speakerKey)
        {
            if(transitioning||IsActive)return Snapshot(false,"已有活动对话，请先结束或取消");
            if(string.IsNullOrWhiteSpace(speakerKey))return Snapshot(false,"对话目标身份缺失");
            var selected=definition.Select(flags);if(selected==null)return Snapshot(false,"当前标记没有匹配的对话段落");
            script=selected;speaker=speakerKey;IsActive=true;Enter(script.EntryNodeId);return Snapshot();
        }
        public CommerceDialogueFrame Continue(int choiceIndex=0)
        {
            if(transitioning)return Snapshot(false,"对话标记正在提交");
            if(!IsActive)return Snapshot(false,"对话已经结束");
            if(current.Kind==CommerceDialogueNodeKind.Choice)
            {
                var choices=current.Choices;if(choiceIndex<0||choiceIndex>=choices.Length)return Snapshot(false,"无效的对话选项");
                Enter(choices[choiceIndex].Next);
            }
            else
            {
                if(choiceIndex!=0)return Snapshot(false,"当前台词没有该选项");Enter(current.Next);
            }
            return Snapshot();
        }
        public CommerceDialogueFrame Cancel()
        {
            if(transitioning)return Snapshot(false,"对话标记正在提交");
            if(IsActive){IsActive=false;current=null;notice="";revision++;}return Snapshot();
        }
        private void Enter(string nodeId)
        {
            transitioning=true;
            try
            {
                var notices=new List<string>();var changed=new List<(string key,bool value)>();var node=script.Node(nodeId);
                while(node!=null&&(node.Kind==CommerceDialogueNodeKind.SetFlag||node.Kind==CommerceDialogueNodeKind.Notice))
                {
                    if(node.Kind==CommerceDialogueNodeKind.SetFlag)
                    {bool mutation=node.FlagValue?flags.Add(node.FlagKey):flags.Remove(node.FlagKey);if(mutation)changed.Add((node.FlagKey,node.FlagValue));}
                    else if(!string.IsNullOrEmpty(node.Text))notices.Add(node.Text);
                    node=script.Node(node.Next);
                }
                current=node;IsActive=node!=null&&node.Kind!=CommerceDialogueNodeKind.End;notice=string.Join("\n",notices);revision++;
                // 完整标记与当前显示节点都已发布；保存观察者失败不撤销分支，也不会重放效果。
                foreach(var change in changed)try{onFlagChanged?.Invoke(change.key,change.value);}catch{NotificationErrorCount++;}
            }
            finally{transitioning=false;}
        }
        private bool HasPresentationAfter(string nodeId)
        {
            var node=script?.Node(nodeId);
            while(node!=null&&(node.Kind==CommerceDialogueNodeKind.SetFlag||node.Kind==CommerceDialogueNodeKind.Notice))node=script.Node(node.Next);
            return node!=null&&node.Kind!=CommerceDialogueNodeKind.End;
        }
        private CommerceDialogueFrame Snapshot(bool ok=true,string reason=null)
        {
            var choices=IsActive&&current.Kind==CommerceDialogueNodeKind.Choice?current.Choices:Array.Empty<CommerceDialogueChoice>();
            return new CommerceDialogueFrame(ok,!IsActive,reason,speaker,script?.Id,current?.Id,IsActive?current.Text:"",choices.Select(c=>c.Text).ToArray(),choices.Select(c=>c.Id).ToArray(),
                IsActive&&(current.Kind==CommerceDialogueNodeKind.Choice||HasPresentationAfter(current.Next)),revision,flags.OrderBy(f=>f,StringComparer.Ordinal).ToArray(),notice);
        }
    }
}
