using System;
using System.Diagnostics;

namespace Ossastra.OSPhysics.Tests
{
    public static class CoreChecks
    {
        public static string Run()
        {
            RunCheck("T01/02 water level and ledger", WaterLevel);
            RunCheck("T03 water-oil, both source orders", Stratification);
            RunCheck("T04/08 sand and water cross an opened gate", GateRelease);
            RunCheck("T05/07 ignition, firebreak, exhausted fuel, ash, smoke", FireAndSmoke);
            RunCheck("T06 localized quenching and ignition above water", Quench);
            RunCheck("T08 cross-chunk sleep and wake", ChunkWake);
            RunCheck("T09 deterministic stepping and reset", DeterministicReset);
            RunCheck("T02 mixed injection/removal never duplicates liquid", MixedLedger);
            RunCheck("T09 detached region reads and author profile", RegionAndCatalog);
            return "9 independent material checks passed (T01–T09); host pause and runtime inputs are separate.";
        }

        private static void RunCheck(string name, Action check)
        {
            try { check(); }
            catch (Exception e) { throw new InvalidOperationException(name + " — " + e.Message, e); }
        }

        private static void WaterLevel()
        {
            var w = Basin(28, 20);
            for (int x = 1; x <= 8; x++) w.SetFixed(x, 10 - x / 2, true);
            for (int x = 9; x <= 12; x++) w.SetFixed(x, 4, true);
            for (int x = 1; x <= 14; x++)
                for (int y = 15; y < 19; y++) w.Add(x, y, Matter.Water, 192);
            int accepted = 14 * 4 * 192;
            Step(w, 1500);
            Require(w.GetLedger(Matter.Water).Current == accepted, "closed stepped pool preserves exact mass");
            Require(At(w, 23, 1).Kind == Matter.Water && At(w, 4, 1).Kind == Matter.Water,
                "water descends below the lip and reaches both ends");
            int a = Surface(w, 16, Matter.Water), b = Surface(w, 25, Matter.Water);
            Require(Math.Abs(a - b) <= 1, "open floor surface levels remain within one cell");
            VerifyLedger(w, Matter.Water);
        }

        private static void Stratification()
        {
            foreach (bool oilFirst in new[] { false, true })
            {
                var w = Basin(24, 20);
                FillRows(w, oilFirst ? Matter.Oil : Matter.Water, 1, 23, 1, 3);
                Step(w, 120);
                FillRows(w, oilFirst ? Matter.Water : Matter.Oil, 1, 23, 8, 10);
                Step(w, 550);
                for (int x = 2; x < 22; x++)
                    Require(At(w, x, 1).Kind == Matter.Water && At(w, x, 4).Kind == Matter.Oil,
                        "water occupies bottom cells and oil occupies the upper layer, both source orders");
                VerifyLedger(w, Matter.Water);
                VerifyLedger(w, Matter.Oil);
            }
        }

        private static void GateRelease()
        {
            foreach (var kind in new[] { Matter.Sand, Matter.Water })
            foreach (bool mirrored in new[] { false, true })
            {
                var w = Basin(48, 22);
                int Map(int x) => mirrored ? w.Width - 1 - x : x;
                for (int x = 0; x < 17; x++) w.SetFixed(Map(x), 7, true);
                for (int y = 7; y < 20; y++) w.SetBarrier(Map(17), y, true);
                for (int y = 11; y < 17; y++)
                    for (int x = 12; x < 16; x++) w.Add(Map(x), y, kind);
                Step(w, 120);
                int near = Math.Min(Map(18), Map(45)), far = Math.Max(Map(18), Map(45)) + 1;
                Require(At(w, Map(15), 8).Kind == kind && Sum(w, kind, near, far, 1, 21) == 0,
                    "closed gate holds everything on the platform");
                for (int y = 7; y < 20; y++) w.SetBarrier(Map(17), y, false);
                Step(w, 200);
                Require(Sum(w, kind, near, far, 1, 8) > 0, "opening gate releases material to lower side");
                VerifyLedger(w, kind);
            }
        }

        private static void FireAndSmoke()
        {
            var w = Basin(52, 30);
            for (int x = 12; x <= 30; x++) w.Add(x, 1, x < 20 ? Matter.Wood : Matter.Grass);
            for (int x = 32; x <= 40; x++) w.Add(x, 1, Matter.Grass);
            w.AddHeat(19, 1, 900);
            bool propagated = false, rose = false;
            for (int i = 0; i < 550; i++)
            {
                w.Step();
                propagated |= At(w, 12, 1).Burning && i < 220;
                rose |= SumSmoke(w, 7) > 0;
            }
            Require(propagated, "fire crosses repeated adjacent fuel cells");
            Require(rose, "smoke rises above the source");
            Require(w.GetLedger(Matter.Ash).Current > 0, "limited ash remains after combustion");
            Require(At(w, 32, 1).Fuel == 255, "one empty cell stops direct ignition");
            Require(w.Metrics.BurningCells == 0 && SumSmoke(w, 0) == 0, "fuel and smoke expire");
            VerifyLedger(w, Matter.Wood);
            VerifyLedger(w, Matter.Grass);

            var scrap = Basin(14, 10);
            scrap.Add(7, 1, Matter.Wood, 5);
            scrap.AddHeat(7, 1, 900);
            Step(scrap, 6);
            Require(scrap.GetLedger(Matter.Ash).Current <= 5, "a partial fuel cell cannot make a full-cell yield");
        }

        private static void Quench()
        {
            var w = Basin(40, 18);
            for (int x = 6; x <= 9; x++) w.Add(x, 1, Matter.Wood);
            for (int x = 22; x <= 26; x++) w.Add(x, 1, Matter.Wood);
            w.AddHeat(7, 1, 900);
            w.AddHeat(24, 1, 900);
            Step(w, 12);
            Require(At(w, 7, 1).Burning && At(w, 24, 1).Burning, "both hot patches ignite");
            w.Add(7, 2, Matter.Water);
            w.Step();
            Require(!At(w, 7, 1).Burning && At(w, 7, 1).Fuel > 0 && At(w, 24, 1).Burning,
                "contact cools a local patch and preserves the distant flame");
            int water = At(w, 7, 2).Amount;
            Require(water > 0, "water is physically in contact while quenching");
            for (int y = 1; y < 7; y++)
                for (int x = 1; x < 18; x++) w.Remove(x, y);
            // Rebuild one location after the other water and residual fuel were removed.
            w.Add(7, 1, Matter.Wood);
            w.AddHeat(7, 1, 900);
            Step(w, 6);
            Require(At(w, 7, 1).Burning, "a new heat input can ignite fuel after water is removed");

            var pool = Basin(18, 12);
            FillRows(pool, Matter.Water, 1, 17, 1, 3);
            FillRows(pool, Matter.Oil, 1, 17, 3, 4);
            pool.AddHeat(8, 3, 900);
            Step(pool, 12);
            Require(At(pool, 8, 3).Burning && pool.GetLedger(Matter.Oil).Reacted > 0,
                "an oil layer can burn on top of a water pool");
            pool.Add(8, 4, Matter.Water);
            pool.Step();
            Require(!At(pool, 8, 4).Burning && pool.LastReactions.Extinguished > 0 &&
                pool.Metrics.BurningCells > 0,
                "poured water cools the oil it displaces without extinguishing the entire surface");
            VerifyLedger(pool, Matter.Oil);
            VerifyLedger(pool, Matter.Water);
        }

        private static void ChunkWake()
        {
            var w = Basin(145, 36);
            for (int y = 1; y < 28; y++)
            {
                w.SetBarrier(64, y, true);
                w.SetFixed(118, y, true);
            }
            FillRows(w, Matter.Water, 58, 63, 1, 7);
            Step(w, 2000);
            Require(w.Metrics.ActiveChunks == 0, "a stable pool and quiet terrain sleep");
            uint quiet = w.GetChunkRevision(4, 0);
            Require(Sum(w, Matter.Water, 65, 96, 1, 35) == 0, "closed gate holds across chunk seam");
            w.SetBarrier(64, 1, false);
            Step(w, 250);
            Require(Sum(w, Matter.Water, 65, 96, 1, 35) > 0, "gate change wakes neighbor and continues flow");
            Require(w.GetChunkRevision(4, 0) == quiet, "sealed distant region remains clean");
            VerifyLedger(w, Matter.Water);

            var boundary = Basin(70, 18);
            boundary.Add(31, 1, Matter.Grass);
            boundary.Add(32, 1, Matter.Grass);
            boundary.AddHeat(31, 1, 750);
            Step(boundary, 12);
            Require(At(boundary, 32, 1).Burning, "fire spreads across a chunk boundary");
        }

        private static void DeterministicReset()
        {
            var a = Basin(46, 20);
            var b = Basin(46, 20);
            foreach (var w in new[] { a, b })
            {
                w.SetBarrier(14, 4, true);
                w.Add(14, 5, Matter.Sand);
                w.Add(20, 8, Matter.Water, 129);
                w.Add(19, 8, Matter.Oil, 173);
                w.Add(6, 1, Matter.Grass);
                w.AddHeat(6, 1, 901);
                Require(w.Tick == 0, "editing does not advance simulation");
                w.Step();
                Require(w.Tick == 1, "exactly one step advances one tick");
                Step(w, 179);
            }
            for (int y = 0; y < a.Height; y++)
                for (int x = 0; x < a.Width; x++)
                    Require(At(a, x, y).Equals(At(b, x, y)), "same seed and input sequence reproduces every cell");
            a.ClearDynamic();
            Require(a.Tick == 0 && At(a, 0, 0).Fixed && At(a, 0, 0).Blocked &&
                !At(a, 14, 4).Barrier && a.GetLedger(Matter.Water).Current == 0 && SumSmoke(a, 0) == 0,
                "clear reinstates rock-only state, zeros ledgers and drops smoke");
            Require(!a.TrySnapshot(-1, 0, out _) && a.Add(-1, 1, Matter.Oil) == 0 &&
                a.Add(0, 0, Matter.Water) == 0 && a.AddHeat(0, 0, 500) == 0, "bounds and protected terrain");
        }

        private static void MixedLedger()
        {
            var w = Basin(64, 43);
            var random = new Random(71);
            long water = 0, oil = 0, waterRemoved = 0, oilRemoved = 0;
            Require(w.Add(4, 1, Matter.Water, 210) == 210 && w.Add(4, 1, Matter.Water, 90) == 45,
                "single-cell addition returns the actual admitted amount");
            Require(!w.SetBarrier(4, 1, true) && !w.SetFixed(4, 1, true) && w.Add(4, 1, Matter.Oil) == 0,
                "obstacle or incompatible liquid cannot overwrite contents");
            water = 255;
            for (int i = 0; i < 875; i++)
            {
                int x = random.Next(2, 62), y = random.Next(1, 32);
                if (i % 3 == 0) water += w.Add(x, y, Matter.Water, random.Next(30, 256));
                else if (i % 3 == 1) oil += w.Add(x, y, Matter.Oil, random.Next(30, 256));
                else
                {
                    var kind = At(w, x, y).Kind;
                    int amount = w.Remove(x, y, 100);
                    if (kind == Matter.Water) waterRemoved += amount;
                    if (kind == Matter.Oil) oilRemoved += amount;
                }
                w.Step();
            }
            Require(Sum(w, Matter.Water, 0, 64, 0, 43) == water - waterRemoved, "actual measured water is conserved");
            Require(Sum(w, Matter.Oil, 0, 64, 0, 43) == oil - oilRemoved, "actual measured oil is conserved");
            VerifyLedger(w, Matter.Water);
            VerifyLedger(w, Matter.Oil);
        }

        private static void RegionAndCatalog()
        {
            var profiles = new System.Collections.Generic.Dictionary<Matter, MatterProperties>
            {
                { Matter.Oil, new MatterProperties(MatterPhase.Liquid, 96, 0, 0) }
            };
            var w = new MatterWorld(16, 12, 1, profiles);
            profiles.Clear();
            w.SetFixed(1, 4, true);
            w.Add(2, 4, Matter.Oil, 150);
            w.Add(3, 4, Matter.Grass, 73);
            var buffer = new MatterSample[5];
            var before = w.GetChunkRevision(0, 0);
            Require(w.CopyRegion(1, 4, 3, 1, buffer) == 3 && buffer[0].Fixed &&
                buffer[1].Amount == 150 && buffer[2].Kind == Matter.Grass &&
                w.GetChunkRevision(0, 0) == before && w.Tick == 0,
                "reading a region returns detached cells in row order and leaves state untouched");
            Require(w.CopyRegion(14, 4, 3, 1, buffer) == 0 && buffer[1].Amount == 150 &&
                w.CopyRegion(1, 4, 3, 1, new MatterSample[2]) == 0,
                "out-of-bounds and destination-too-small reads preserve the prior result");
            w.Step();
            Require(At(w, 2, 4).Kind == Matter.Oil && At(w, 2, 3).Kind == Matter.Empty,
                "changing the source dictionary does not change the copied runtime flow profile");
            w.Remove(2, 4);
            Require(buffer[1].Amount == 150, "existing snapshots do not follow future world changes");
        }

        public static string Benchmark(int samples = 90)
        {
            if (samples < 1) throw new ArgumentOutOfRangeException(nameof(samples));
            var report = new System.Text.StringBuilder("size case samples ms(P50/P95) activeCells GC-bytes");
            foreach (int width in new[] { 256, 512, 1024 })
                foreach (string scenario in new[] { "empty", "settled", "local", "dense" })
                {
                    var w = Basin(width, width / 2);
                    if (scenario == "settled")
                    {
                        FillRows(w, Matter.Water, 1, width - 1, 1, 3);
                        Step(w, 12);
                    }
                    if (scenario == "local")
                        FillRows(w, Matter.Water, 16, 26, 8, 30);
                    if (scenario == "dense")
                        for (int y = 1; y < w.Height - 1; y++)
                            for (int x = 1; x < w.Width - 1; x++)
                            {
                                Matter kind = (x / 8 % 2 == 0) ? Matter.Wood : Matter.Oil;
                                w.Add(x, y, kind);
                                w.AddHeat(x, y, 900);
                            }
                    var times = new double[samples];
                    long active = 0;
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    for (int i = 0; i < samples; i++)
                    {
                        // Maintain a live workload across samples. The dense case is a stress fixture;
                        // re-seed consumed cells every 20 ticks and time the complete solver step separately.
                        if (scenario == "local") w.Add(21, 31, Matter.Water);
                        if (scenario == "dense" && i % 20 == 0)
                            for (int y = 1; y < w.Height - 1; y++)
                                for (int x = 1; x < w.Width - 1; x++)
                                {
                                    var kind = x / 8 % 2 == 0 ? Matter.Wood : Matter.Oil;
                                    w.Remove(x, y);
                                    w.Add(x, y, kind);
                                    w.AddHeat(x, y, 900);
                                }
                        w.Step();
                        times[i] = w.Metrics.StepMilliseconds;
                        active += w.Metrics.ActiveCells;
                    }
                    long allocations = GC.GetAllocatedBytesForCurrentThread() - before;
                    Array.Sort(times);
                    report.Append($"{Environment.NewLine}{width}x{w.Height} {scenario} {samples} " +
                        $"{times[(samples - 1) / 2]:F4}/{times[(int)Math.Ceiling(samples * .95) - 1]:F4} " +
                        $"{active / samples} {allocations}");
                }
            return report.ToString();
        }

        private static MatterWorld Basin(int width, int height)
        {
            var w = new MatterWorld(width, height, 317);
            for (int x = 0; x < width; x++) w.SetFixed(x, 0, true);
            for (int y = 1; y < height; y++)
            {
                w.SetFixed(0, y, true);
                w.SetFixed(width - 1, y, true);
            }
            return w;
        }

        private static void FillRows(MatterWorld w, Matter kind, int x0, int x1, int y0, int y1)
        {
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++) w.Add(x, y, kind);
        }

        private static int Surface(MatterWorld w, int x, Matter kind)
        {
            for (int y = w.Height - 1; y >= 0; y--)
                if (At(w, x, y).Kind == kind) return y;
            return -1;
        }

        private static long Sum(MatterWorld w, Matter kind, int x0, int x1, int y0, int y1)
        {
            long sum = 0;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    var sample = At(w, x, y);
                    if (sample.Kind == kind) sum += sample.Amount;
                }
            return sum;
        }

        private static long SumSmoke(MatterWorld w, int y0)
        {
            long sum = 0;
            for (int y = y0; y < w.Height; y++)
                for (int x = 0; x < w.Width; x++) sum += At(w, x, y).Smoke;
            return sum;
        }

        private static MatterSample At(MatterWorld w, int x, int y)
        {
            Require(w.TrySnapshot(x, y, out var sample), "fixture position inside simulation");
            return sample;
        }

        private static void VerifyLedger(MatterWorld w, Matter kind)
        {
            var ledger = w.GetLedger(kind);
            Require(ledger.Current == ledger.Initial + ledger.Injected + ledger.Produced - ledger.Removed -
                ledger.Reacted - ledger.Lost, "ledger closes exactly");
            Require(ledger.Current == Sum(w, kind, 0, w.Width, 0, w.Height), "ledger matches sampled grid");
        }

        private static void Step(MatterWorld w, int ticks)
        {
            for (int i = 0; i < ticks; i++) w.Step();
        }

        private static void Require(bool value, string reason)
        {
            if (!value) throw new InvalidOperationException(reason);
        }
    }
}

