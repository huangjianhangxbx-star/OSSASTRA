using System;

namespace Ossastra.OSPhysics
{
    internal static class MatterLabClock
    {
        internal const int Frequency = 30;
        internal const int MaxSteps = 4;
        internal const double Interval = 1.0 / Frequency;

        internal static int Take(double elapsed, double speed, bool active, out double remainder, double carry = 0)
        {
            if (!active)
            {
                remainder = 0;
                return 0;
            }

            double budget = Math.Min(elapsed * speed + carry, Interval * MaxSteps);
            int steps = (int)(budget / Interval);
            remainder = budget - steps * Interval;
            return steps;
        }
    }

    internal static class MatterStroke
    {
        // Integer grid walk. Share each tick's brush budget across the stamps.
        internal static int Trace(int x, int y, int targetX, int targetY, int spacing, int amount,
            Func<int, int, int, int> stamp)
        {
            int dx = Math.Abs(targetX - x);
            int dy = Math.Abs(targetY - y);
            int distance = Math.Max(dx, dy);
            int stamps = distance == 0 ? 1 : (distance + spacing - 1) / spacing + 1;
            int accepted = 0;
            for (int i = 0; i < stamps; i++)
            {
                double t = stamps == 1 ? 0 : (double)i / (stamps - 1);
                int share = (int)((long)(i + 1) * amount / stamps - (long)i * amount / stamps);
                accepted += stamp((int)Math.Round(x + (targetX - x) * t),
                    (int)Math.Round(y + (targetY - y) * t), share);
            }

            return accepted;
        }
    }
}
