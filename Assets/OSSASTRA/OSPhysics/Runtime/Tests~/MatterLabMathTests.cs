using System;
using Ossastra.OSPhysics;

static class MatterLabMathTests
{
    static void Main()
    {
        Require(MatterLabClock.Take(0.4, 1, false, out _) == 0, "paused has no steps");
        Require(MatterLabClock.Take(0.4, 1, false, out var paused) == 0 && paused == 0,
            "paused discards debt");
        Require(MatterLabClock.Take(0.5, 1, true, out _) == 4, "hitch has bounded work");
        Require(MatterLabClock.Take(1.0 / 60, 2, true, out _) == 1, "2x advances on a 60 Hz frame");
        Require(MatterLabClock.Take(1.0 / 60, 0.5, true, out _) == 0, "0.5x accumulates fractions");
        int slowTicks = 0;
        double carry = 0;
        for (int i = 0; i < 60; i++)
            slowTicks += MatterLabClock.Take(1.0 / 60, .5, true, out carry, carry);
        Require(slowTicks == 15, "0.5x preserves fractional time across frames");

        int count = 0;
        int lastX = -1;
        int lastY = -1;
        MatterStroke.Trace(1, 8, 21, 3, 1, 168, (x, y, amount) =>
        {
            if (count > 0) Require(Math.Abs(x - lastX) <= 1 && Math.Abs(y - lastY) <= 1,
                "rapid diagonal stroke has no grid gaps");
            lastX = x;
            lastY = y;
            count++;
            return amount;
        });
        Require(count == 21 && lastX == 21 && lastY == 3, "line reaches cursor");
        int stationary = MatterStroke.Trace(1, 8, 1, 8, 1, 168, (_, _, amount) => amount);
        int moving = MatterStroke.Trace(1, 8, 21, 3, 1, 168, (_, _, amount) => amount);
        Require(stationary == moving && moving == 168, "line and stationary strokes share a per-step rate");
        Console.WriteLine("Matter lab clock and brush: PASS");
    }

    static void Require(bool truth, string message)
    {
        if (!truth) throw new Exception(message);
    }
}
