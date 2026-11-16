using Ossastra.Characters;
using static Ossastra.Characters.PosturePadButtons;
using static Ossastra.Characters.PostureInputCommand;
using P = Ossastra.Characters.PosturePadButtons;
using C = Ossastra.Characters.PostureInputCommand;

var edges = new List<PosturePadEdge>();
var pad = new PostureInputGestures();
void Step(C[] expected, params P[] states)
{
    edges.Clear();
    pad.Resolve(states, true, edges);
    var pressed = edges.Where(e => e.Pressed).Select(e => e.Command).ToArray();
    if (!pressed.SequenceEqual(expected))
        throw new InvalidOperationException($"Presses [{string.Join(",", pressed)}], expected [{string.Join(",", expected)}]");
}
Step(new[] { QuickSlotOne }, RightShoulder | X);
Step(Array.Empty<C>(), X);
Step(new[] { MainHand }, P.None, X);
Step(Array.Empty<C>(), X | LeftShoulder);
Step(Array.Empty<C>(), P.None);
Step(new[] { SkillTwo }, Y, Y | LeftShoulder);
Step(Array.Empty<C>(), P.None);
Step(new[] { SwitchCharacter }, RightShoulder, RightShoulder | X, RightShoulder | LeftShoulder | X);
Step(Array.Empty<C>(), LeftShoulder | A, LeftShoulder);
Step(new[] { Heal }, P.None, LeftShoulder | A);
Step(Array.Empty<C>(), P.None);
Step(new[] { SwitchCharacter }, RightShoulder | X, RightShoulder, RightShoulder | LeftShoulder);
Step(Array.Empty<C>(), P.None);
Step(new[] { WeaponMode }, RightTrigger | LeftTrigger);
Step(Array.Empty<C>(), RightTrigger);
Step(new[] { Offhand }, P.None, RightTrigger);
Step(new[] { WeaponMode }, RightTrigger | LeftTrigger);
Step(Array.Empty<C>(), LeftTrigger);
if (edges.Count != 1 || edges[0].Pressed || edges[0].Command != Offhand)
    throw new InvalidOperationException("Accepted offhand must see real release even after mode chord.");
Step(Array.Empty<C>(), P.None);
Step(new[] { Jump }, A, P.None);
if (edges.Count != 2 || edges[1].Pressed || edges[1].Command != Jump)
    throw new InvalidOperationException("Jump tap must preserve the release in the same update.");
Step(new[] { Jump }, A);
pad.Reset(A | Up, preserveJump: true);
Step(Array.Empty<C>(), A | Up, Up);
if (edges.Count != 1 || edges[0].Pressed || edges[0].Command != Jump)
    throw new InvalidOperationException("Active jump must see release across capture.");
Step(new[] { Interact }, P.None, Up);
Step(Array.Empty<C>(), Up);
Step(new[] { Jump }, Up | A);
pad.Reset(A);
Step(Array.Empty<C>(), A);
Step(new[] { Jump }, P.None, A);
Step(Array.Empty<C>(), P.None, LeftShoulder | X); // LB+X is reserved and consumed.
Step(Array.Empty<C>(), P.None);
Step(new[] { SkillThree }, LeftShoulder | B);
Step(Array.Empty<C>(), P.None);
Step(new[] { QuickSlotTwo }, RightShoulder | Y);
Step(Array.Empty<C>(), P.None);
Step(new[] { QuickSlotThree }, RightShoulder | B);
Step(Array.Empty<C>(), P.None);
Step(new[] { QuickSlotFour }, RightShoulder | A);
Step(Array.Empty<C>(), P.None);
Step(new[] { SkillOne }, Y);
Step(Array.Empty<C>(), P.None);
Step(new[] { Dodge }, B);
Step(Array.Empty<C>(), P.None);
Step(new[] { MainHand }, X, P.None, LeftShoulder); // Non-overlapping intervals in one update cannot compose.
Step(Array.Empty<C>(), P.None);
edges.Clear();
pad.Resolve(new[] { A | RightShoulder }, false, edges);
if (edges.Count != 0) throw new InvalidOperationException("Capture cannot issue new gameplay intents.");
Step(Array.Empty<C>(), A);
Step(Array.Empty<C>(), P.None);
Step(new[] { Jump }, A);

var otherPad = new PostureInputGestures();
edges.Clear();
otherPad.Resolve(new[] { LeftShoulder }, true, edges);
pad.Reset(P.None);
Step(new[] { SkillOne }, Y); // The other device's LB cannot modify this device.
Console.WriteLine("PASS ordered input updates, including chord ownership and physical release");
