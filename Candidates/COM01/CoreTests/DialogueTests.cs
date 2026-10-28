/* 测试：固定原型SAMPLE三句、A/B分支、持久标记及重访；故事文本只在测试夹具，不写入生产会话机。 */
using System;
using System.Collections.Generic;
using Ossastra.Commerce;
static class DialogueTests
{
    static void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static CommerceDialogueDefinition Fixture()
    {
        return new CommerceDialogueDefinition("companion",new[]{
            new CommerceDialogueScript("sample.companion.default",0,"greeting",new[]{
                new CommerceDialogueNode("greeting",CommerceDialogueNodeKind.Line,"我在这里，准备好继续探索了吗？","team"),
                new CommerceDialogueNode("team",CommerceDialogueNodeKind.Line,"前面的路还不熟悉，我们一起留意周围吧。","plan"),
                new CommerceDialogueNode("plan",CommerceDialogueNodeKind.Line,"出发前，我想听听你的打算。","choice"),
                new CommerceDialogueNode("choice",CommerceDialogueNodeKind.Choice,"接下来，要先检查补给还是观察前路？",choices:new[]{new CommerceDialogueChoice("A","检查补给","flagA"),new CommerceDialogueChoice("B","观察前路","flagB")}),
                new CommerceDialogueNode("flagA",CommerceDialogueNodeKind.SetFlag,next:"noticeA",flagKey:"sample.dialogue.choiceA"),
                new CommerceDialogueNode("noticeA",CommerceDialogueNodeKind.Notice,"SAMPLE · 已记录检查补给的选择","endA"),
                new CommerceDialogueNode("endA",CommerceDialogueNodeKind.Line,"好，先检查补给，我会留意附近的动静。"),
                new CommerceDialogueNode("flagB",CommerceDialogueNodeKind.SetFlag,next:"noticeB",flagKey:"sample.dialogue.choiceB"),
                new CommerceDialogueNode("noticeB",CommerceDialogueNodeKind.Notice,"SAMPLE · 已记录观察前路的选择","endB"),
                new CommerceDialogueNode("endB",CommerceDialogueNodeKind.Line,"那就先看看前路，我们保持彼此照应。")}),
            new CommerceDialogueScript("sample.companion.afterA",10,"first",new[]{new CommerceDialogueNode("first",CommerceDialogueNodeKind.Line,"你刚才说要检查补给，我记住了。","last"),new CommerceDialogueNode("last",CommerceDialogueNodeKind.Line,"准备好了就继续走吧，我会跟着你。")},new[]{new CommerceDialogueCondition("sample.dialogue.choiceA")}),
            new CommerceDialogueScript("sample.companion.afterB",10,"first",new[]{new CommerceDialogueNode("first",CommerceDialogueNodeKind.Line,"你刚才说要观察前路，我记住了。","last"),new CommerceDialogueNode("last",CommerceDialogueNodeKind.Line,"准备好了就继续走吧，我会跟着你。")},new[]{new CommerceDialogueCondition("sample.dialogue.choiceB")})});
    }
    static CommerceDialogueFrame ToChoice(CommerceDialogue flow)
    {
        var first=flow.Start("freya-runtime-id");Require(first.Ok&&first.Text=="我在这里，准备好继续探索了吗？"&&first.SpeakerKey=="freya-runtime-id","首句或实际角色别名错误");
        Require(flow.Continue().Text=="前面的路还不熟悉，我们一起留意周围吧。","第二句缺失");
        Require(flow.Continue().Text=="出发前，我想听听你的打算。","第三句缺失");
        var choice=flow.Continue();Require(choice.Choices.Length==2&&choice.Choices[0]=="检查补给"&&choice.Choices[1]=="观察前路","A/B选择没有出现");return choice;
    }
    internal static void Run(Action<string,Action> test)
    {
        test("dialogue_three_lines_choice_A_flag_notice_and_revisit",()=>{var flags=new HashSet<string>();int saved=0;var flow=new CommerceDialogue(Fixture(),flags,(key,value)=>saved++);ToChoice(flow);var end=flow.Continue(0);Require(end.Ok&&!end.Completed&&!end.HasNext&&end.Text=="好，先检查补给，我会留意附近的动静。"&&end.Notice.Contains("检查补给")&&flags.Contains("sample.dialogue.choiceA")&&!flags.Contains("sample.dialogue.choiceB")&&saved==1,"A分支文本/标记/保存通知不完整");Require(flow.Continue().Completed,"末句之后未结束");var next=new CommerceDialogue(Fixture(),new HashSet<string>(end.Flags));Require(next.Start("freya-runtime-id").Text=="你刚才说要检查补给，我记住了。"&&next.Continue().Text=="准备好了就继续走吧，我会跟着你。"&&next.Continue().Completed,"持久flag重访缺失");});
        test("dialogue_choice_B_has_distinct_flag_and_revisit",()=>{var flags=new HashSet<string>();var flow=new CommerceDialogue(Fixture(),flags);ToChoice(flow);var end=flow.Continue(1);Require(end.Text=="那就先看看前路，我们保持彼此照应。"&&flags.Contains("sample.dialogue.choiceB")&&!flags.Contains("sample.dialogue.choiceA"),"B分支未实现");flow.Continue();Require(flow.Start("freya-runtime-id").Text=="你刚才说要观察前路，我记住了。","B重访未选择");});
        test("dialogue_invalid_choice_does_not_advance_or_set_flags",()=>{var flags=new HashSet<string>();var flow=new CommerceDialogue(Fixture(),flags);var choice=ToChoice(flow);var denied=flow.Continue(99);Require(!denied.Ok&&denied.Revision==choice.Revision&&flags.Count==0&&flow.Continue(1).Ok,"非法选项仍提交效果");});
        test("dialogue_cancel_does_not_commit_unselected_branch",()=>{var flags=new HashSet<string>();var flow=new CommerceDialogue(Fixture(),flags);ToChoice(flow);Require(flow.Cancel().Completed&&!flow.IsActive&&flags.Count==0,"取消对话写入分支flag");});
        test("dialogue_callback_failure_does_not_repeat_or_undo_effect",()=>{var flags=new HashSet<string>();var flow=new CommerceDialogue(Fixture(),flags,(key,value)=>throw new Exception("save observer"));ToChoice(flow);Require(flow.Continue(0).Ok&&flow.NotificationErrorCount==1&&flags.Contains("sample.dialogue.choiceA"),"通知故障取消了已提交标记");flow.Continue();Require(flow.NotificationErrorCount==1,"末句重复提交效果");});
        test("dialogue_effect_cycle_and_missing_route_are_diagnostic",()=>{bool cycle=false,missing=false;try{new CommerceDialogueDefinition("bad",new[]{new CommerceDialogueScript("bad",0,"a",new[]{new CommerceDialogueNode("a",CommerceDialogueNodeKind.SetFlag,next:"a",flagKey:"x")})});}catch(ArgumentException){cycle=true;}try{new CommerceDialogueDefinition("bad",new[]{new CommerceDialogueScript("bad",0,"a",new[]{new CommerceDialogueNode("a",CommerceDialogueNodeKind.Line,"a","missing")})});}catch(ArgumentException){missing=true;}Require(cycle&&missing,"效果环/缺失节点未报配置错误");});
        test("dialogue_tied_priority_matches_original_catalog_order",()=>{var flow=new CommerceDialogue(Fixture(),new HashSet<string>{"sample.dialogue.choiceA","sample.dialogue.choiceB"});Require(flow.Start("freya").DefinitionId=="sample.companion.afterA","同优先级选择不稳定");});
        test("dialogue_selector_uses_condition_specificity_then_ordinal_id",()=>{var line=new[]{new CommerceDialogueNode("line",CommerceDialogueNodeKind.Line,"文本")};var scripts=new[]{new CommerceDialogueScript("z",10,"line",line,new[]{new CommerceDialogueCondition("x")}),new CommerceDialogueScript("a",10,"line",line,new[]{new CommerceDialogueCondition("x"),new CommerceDialogueCondition("y")})};var definition=new CommerceDialogueDefinition("pool",scripts);Require(new CommerceDialogue(definition,new HashSet<string>{"x","y"}).Start("freya").DefinitionId=="a","未按原型条件数降序");definition=new CommerceDialogueDefinition("pool",new[]{scripts[0],new CommerceDialogueScript("b",10,"line",line,new[]{new CommerceDialogueCondition("x")})});Require(new CommerceDialogue(definition,new HashSet<string>{"x"}).Start("freya").DefinitionId=="b","未按原型ID升序打平");});
    }
}
