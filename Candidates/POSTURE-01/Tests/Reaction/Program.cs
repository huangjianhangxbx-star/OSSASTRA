using System;
using Ossastra.CharacterCore;

static void Expect(bool actual, string detail)
{
    if (!actual) throw new Exception(detail);
}

var reaction = new PostureReactionState();
var action = Guid.NewGuid();
Expect(reaction.Begin(action, -1, 8f, 18f, .2f, .45f, .25f), "launch begins from inactive");
Expect(reaction.Phase == PostureReactionPhase.Launched && reaction.ActionId == action, "reaction owns exact hit");
var plan = reaction.PlanStep(.02f);
Expect(plan.Horizontal < -2.7f && plan.LaunchVertical > 7f, "direction and relative jump speeds");
Expect(!reaction.Begin(Guid.NewGuid(), 1, 20f, 30f, .2f, .45f, .25f), "later hits cannot replace an active launch");
reaction.ObserveStep(.02f, false, true);
Expect(reaction.Phase == PostureReactionPhase.Launched, "an airborne wall collision is not a landing");
plan = reaction.PlanStep(.02f);
Expect(plan.Horizontal == 0 && !plan.LaunchVertical.HasValue, "blocked horizontal motion stops without replaying upward impulse");
for (var index = 0; index < 50; index++)
{
    reaction.PlanStep(.02f);
    reaction.ObserveStep(.02f, false, false);
}
Expect(reaction.IsActive && reaction.Phase == PostureReactionPhase.Launched, "time alone cannot stand in for actual support");
reaction.ObserveStep(.02f, true, false);
Expect(reaction.Phase == PostureReactionPhase.Downed, "real support enters downed");
reaction.ObserveStep(.2f, true, false);
Expect(reaction.Phase == PostureReactionPhase.Downed, "downed has independent duration");
reaction.ObserveStep(.3f, true, false);
Expect(reaction.Phase == PostureReactionPhase.GettingUp, "supported downed time starts getting up");
reaction.ObserveStep(.1f, false, false);
Expect(reaction.Phase == PostureReactionPhase.Launched && !reaction.PlanStep(.02f).LaunchVertical.HasValue, "losing ground while getting up returns to falling without another impulse");
reaction.ObserveStep(.02f, true, false);
reaction.ObserveStep(.45f, true, false);
reaction.ObserveStep(.25f, true, false);
Expect(!reaction.IsActive && reaction.ActionId == Guid.Empty, "release after actual landing downed and getting up");
Expect(reaction.Begin(Guid.NewGuid(), 0, 8f, 18f, .2f, .45f, .25f), "environment fallback has explicit direction");
Expect(reaction.PlanStep(.02f).Horizontal > 2.7f, "fallback is +X");
reaction.Reset();
Expect(!reaction.IsActive && reaction.PlanStep(.02f).Horizontal == 0, "death/reset drops owned velocities");
Console.WriteLine("PASS: reaction identity, direction, exclusive launch, wall, actual support, interruption, and recovery.");
