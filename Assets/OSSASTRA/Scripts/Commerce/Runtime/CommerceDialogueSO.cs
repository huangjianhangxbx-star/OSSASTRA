/* 脚本：CommerceDialogueSO
 * 功能：作者配置对话内容池、优先条件、台词/选项/标记与提示路由；Freeze转成经验证的纯数据图。
 * 边界：不存运行会话或玩家标记、不写硬编码剧情；具体文本与分支全部保存在本资产。
 * 协作：MerchantShop引用本SO；CommerceService将Freeze结果交给CommerceDialogue并接统一保存。
 */
using System;
using System.Linq;
using UnityEngine;
namespace Ossastra.Commerce.Runtime
{
    [Serializable] public sealed class CommerceDialogueConditionConfig {public string flag;public bool expected=true;}
    [Serializable] public sealed class CommerceDialogueChoiceConfig {public string id,text,next;}
    [Serializable] public sealed class CommerceDialogueNodeConfig
    {
        public string id;
        public CommerceDialogueNodeKind kind;
        [TextArea] public string text;
        public string next,flagKey;
        public bool flagValue=true;
        public CommerceDialogueChoiceConfig[] choices=Array.Empty<CommerceDialogueChoiceConfig>();
    }
    [Serializable] public sealed class CommerceDialogueScriptConfig
    {
        public string id,entryNodeId;
        public int priority;
        public CommerceDialogueConditionConfig[] conditions=Array.Empty<CommerceDialogueConditionConfig>();
        public CommerceDialogueNodeConfig[] nodes=Array.Empty<CommerceDialogueNodeConfig>();
    }
    [CreateAssetMenu(menuName="OSSASTRA/Commerce/对话内容池")]
    public sealed class CommerceDialogueSO:ScriptableObject
    {
        public string definitionId="dialogue-pool";
        public CommerceDialogueScriptConfig[] scripts=Array.Empty<CommerceDialogueScriptConfig>();
        public CommerceDialogueDefinition Freeze()
        {
            if(scripts==null||scripts.Any(s=>s==null||s.nodes==null||s.conditions==null))throw new ArgumentException("对话段落、节点或条件配置缺失");
            return new CommerceDialogueDefinition(definitionId,scripts.Select(s=>new CommerceDialogueScript(s.id,s.priority,s.entryNodeId,s.nodes.Select(n=>
            {
                if(n==null||n.choices==null||n.choices.Any(c=>c==null))throw new ArgumentException("对话节点或选项配置缺失");
                return new CommerceDialogueNode(n.id,n.kind,n.text,n.next,n.choices.Select(c=>new CommerceDialogueChoice(c.id,c.text,c.next)).ToArray(),n.flagKey,n.flagValue);
            }).ToArray(),s.conditions.Select(c=>c==null?throw new ArgumentException("对话条件缺失"):new CommerceDialogueCondition(c.flag,c.expected)).ToArray())).ToArray());
        }
    }
}
