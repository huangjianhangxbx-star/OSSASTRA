using System;

namespace Ossastra.OSPhysics
{
    public enum Matter : byte { Empty, Water, Oil, Sand, Grass, Wood, Ash }
    public enum MatterPhase : byte { Static, Liquid, Granular }

    public readonly struct MatterProperties
    {
        public readonly MatterPhase Phase;
        public readonly int Density, DownwardRate, LateralRate;
        public readonly int IgnitionHeat, BurnRate, HeatSpread, AshYield, SmokePerFuel;

        public MatterProperties(MatterPhase phase, int density, int downwardRate, int lateralRate,
            int ignitionHeat = 0, int burnRate = 0, int heatSpread = 0, int ashYield = 0, int smokePerFuel = 0)
        {
            Phase = phase;
            Density = Limit(density, 0, 255);
            DownwardRate = Limit(downwardRate, 0, 255);
            LateralRate = Limit(lateralRate, 0, 255);
            IgnitionHeat = Limit(ignitionHeat, 0, MatterWorld.MaxHeat);
            BurnRate = Limit(burnRate, 0, 255);
            HeatSpread = Limit(heatSpread, 0, MatterWorld.MaxHeat);
            AshYield = Limit(ashYield, 0, 255);
            SmokePerFuel = Limit(smokePerFuel, 0, 255);
        }

        private static int Limit(int value, int minimum, int maximum) => Math.Max(minimum, Math.Min(maximum, value));
    }

    public readonly struct MatterSample
    {
        public readonly Matter Kind;
        public readonly int Amount, Heat, Fuel, Smoke;
        public readonly bool Burning, Fixed, Barrier;
        public bool Blocked => Fixed || Barrier;

        internal MatterSample(Matter kind, int amount, int heat, int fuel, int smoke, bool burning, bool rock, bool barrier)
        {
            Kind = kind;
            Amount = amount;
            Heat = heat;
            Fuel = fuel;
            Smoke = smoke;
            Burning = burning;
            Fixed = rock;
            Barrier = barrier;
        }
    }

    public struct MatterLedger
    {
        public long Initial { get; internal set; }
        public long Injected { get; internal set; }
        public long Produced { get; internal set; }
        public long Removed { get; internal set; }
        public long Reacted { get; internal set; }
        public long Lost { get; internal set; }
        public long Current { get; internal set; }
    }

    public readonly struct MatterMetrics
    {
        public readonly int ActiveChunks, ActiveCells, TotalCells, BurningCells, DirtyChunks;
        public readonly double StepMilliseconds;

        internal MatterMetrics(int activeChunks, int activeCells, int totalCells, int burningCells,
            int dirtyChunks, double stepMilliseconds)
        {
            ActiveChunks = activeChunks;
            ActiveCells = activeCells;
            TotalCells = totalCells;
            BurningCells = burningCells;
            DirtyChunks = dirtyChunks;
            StepMilliseconds = stepMilliseconds;
        }
    }

    public readonly struct MatterReactions
    {
        public readonly int Ignitions, Extinguished, Exhausted, FuelBurned, SmokeProduced;

        internal MatterReactions(int ignitions, int extinguished, int exhausted, int fuelBurned, int smokeProduced)
        {
            Ignitions = ignitions;
            Extinguished = extinguished;
            Exhausted = exhausted;
            FuelBurned = fuelBurned;
            SmokeProduced = smokeProduced;
        }
    }
}

