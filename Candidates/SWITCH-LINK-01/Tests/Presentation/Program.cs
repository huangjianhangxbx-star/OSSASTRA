using System;
using Ossastra.Characters;

static void Check(bool success, string detail)
{
    if (!success) throw new Exception(detail);
}

static void Near(float actual, float expected, string detail)
    => Check(Math.Abs(actual - expected) < .0001f, $"{detail}. Expected {expected}, received {actual}");

var clock = new SwitchLinkPresentationClock();
var id = Guid.NewGuid();
Check(clock.TryBegin(id, 10, .06f, .15f, .24f, 1f), "committed exchange starts presentation");
Check(!clock.TryBegin(id, 10.03, .06f, .15f, .24f, 1f), "duplicate id does not restart envelope");
Check(!clock.TryBegin(Guid.NewGuid(), 10.03, .06f, .15f, .24f, 1f), "another request cannot stack during an exchange");
Check(clock.TrySample(10.03, out float entering), "exchange enters while real elapsed time is within .06 s");
Near(entering, .5f, "ease at half entrance");
Check(clock.TrySample(10.14, out float holding), "hold lasts through the middle of 0.15 s");
Near(holding, 1f, "full screen magnification during hold");
Check(clock.TrySample(10.33, out float leaving), "leaves with 0.24 s unscaled recovery");
Near(leaving, .5f, "ease at half recovery");
Check(!clock.TrySample(10.451, out _), "real timeline expires independent of slowed combat rate");
Near(clock.Release(1f), 1f, "unused time writer leaves prior base");
Check(!clock.TryBegin(id, 11, .06f, .15f, .24f, 1f), "old request cannot reenter after release");
Console.WriteLine("PASS real-time envelope, deduplication and single request");

Check(clock.TryBegin(Guid.NewGuid(), 12, .06f, .15f, .24f, 1.2f), "new committed exchange acquires lease");
Near(clock.ApplyTime(1.2f, .65f, 1f), .65f, "exact authored slowdown from a pre-existing speed");
Near(clock.Release(.65f), 1.2f, "cancellation restores the previous speed");
Check(!clock.TrySample(12.1, out _), "cancellation also releases the zoom envelope");
Console.WriteLine("PASS cancellation restores only the owned timescale and releases the envelope");

Check(clock.TryBegin(Guid.NewGuid(), 13, .06f, .15f, .24f, .5f), "stronger active slowdown is supported");
Near(clock.ApplyTime(.5f, .65f, 1f), .5f, "stronger pre-existing slowdown takes precedence");
Near(clock.Release(.5f), .5f, "stronger external slowdown remains");
Check(!clock.TryBegin(Guid.NewGuid(), 14, .06f, .15f, .24f, 0f), "paused combat refuses a new time request");
Check(clock.TryBegin(Guid.NewGuid(), 14, .06f, .15f, .24f, 1f), "another exchange may run at normal speed");
Near(clock.ApplyTime(1f, .65f, 1f), .65f, "exchange changes before external priority request");
Near(clock.ApplyTime(0f, .65f, 1f), 0f, "external pause remains authoritative");
Near(clock.ApplyTime(.4f, .65f, .5f), .4f, "an external writer remains authoritative for this lease");
Near(clock.Release(.4f), .4f, "releasing the exchange preserves another writer's scale");
Console.WriteLine("PASS externally owned slowdown and pause retain priority");

Near(SwitchLinkPresentationClock.OrthographicSize(6f, 1.2f), 5f, "orthographic bounds cover a smaller canvas");
Near(SwitchLinkPresentationClock.PerspectiveFieldOfView(90f, 2f), 53.130102f, "perspective zoom scales tangent, not field angle");
Near(SwitchLinkPresentationClock.PerspectiveFieldOfView(60f, 1f), 60f, "neutral frame uses original perspective projection");
Console.WriteLine("PASS orthographic and perspective zoom geometry");
