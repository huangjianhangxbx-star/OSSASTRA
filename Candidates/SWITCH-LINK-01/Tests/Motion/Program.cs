using System;
using Ossastra.Characters;

int failures = 0;
Check("clear path accepts full retreat", () => Near(SideViewSwitchPath.ReachableDistance(-.55f, .05f, _ => true), -.55f));
Check("capsule blocked at first segment allows zero retreat", () => Near(SideViewSwitchPath.ReachableDistance(.55f, .05f, _ => false), 0));
Check("path cannot skip the first wall to reach a free endpoint", () =>
    Near(SideViewSwitchPath.ReachableDistance(.55f, .05f, distance => distance < .26f || distance > .32f), .25f));
Check("left retreat stops at ledge before a lower platform", () =>
    Near(SideViewSwitchPath.ReachableDistance(-.50f, .05f, distance => distance > -.24f || distance < -.4f), -.20f));
Check("exact placement wins", () =>
    SideViewSwitchPath.TryFindPlacementOffset(.08f, .02f, _ => true, out var x) && Near(x, 0));
Check("nearest valid placement wins without jumping to an outer offset", () =>
    SideViewSwitchPath.TryFindPlacementOffset(.08f, .02f, x => x > .015f || x < -.035f, out var x) && Near(x, .02f));
Check("placement beyond body size correction is rejected", () =>
    !SideViewSwitchPath.TryFindPlacementOffset(.08f, .02f, x => x > .09f, out _));
Check("height continuity admits slopes and rejects positive or negative step", () =>
    SideViewSwitchPath.KeepsSupport(.04f, .035f, 1f, .01f) &&
    !SideViewSwitchPath.KeepsSupport(.04f, .065f, 1f, .01f) &&
    !SideViewSwitchPath.KeepsSupport(.04f, -.08f, 1f, .01f));
return failures == 0 ? 0 : 1;

void Check(string name, Func<bool> condition)
{
    bool ok = condition();
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}");
}

static bool Near(float value, float expected) => Math.Abs(value - expected) < .001f;
