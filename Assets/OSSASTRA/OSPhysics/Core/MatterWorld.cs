using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ossastra.OSPhysics
{
    /// <summary>Finite, sealed XY grid. The caller owns the clock and world-space conversion.</summary>
    public sealed partial class MatterWorld
    {
        public const int ChunkSize = 32;
        public const int MaxHeat = 1023;
        private const int Capacity = 255;
        private const byte FixedRock = 1, TestBarrier = 2, Flame = 4, ColdContact = 8;
        private const byte MotionBlocked = FixedRock | TestBarrier;
        private const byte QuietSteps = 4;
        private const int MatterCount = (int)Matter.Ash + 1;

        private struct Cell
        {
            public ushort Heat, AshResidue;
            public byte Mass, Smoke, Flags;
            public Matter Kind;
            public bool IsBlocked => (Flags & MotionBlocked) != 0;
            public bool IsBurning => (Flags & Flame) != 0;
        }

        private static readonly MatterProperties[] Defaults =
        {
            default, // Empty
            new MatterProperties(MatterPhase.Liquid, 128, 255, 104), // Water
            new MatterProperties(MatterPhase.Liquid, 95, 104, 42, 138, 3, 55, 0, 6), // Oil
            new MatterProperties(MatterPhase.Granular, 230, 255, 255), // Sand
            new MatterProperties(MatterPhase.Static, 70, 0, 0, 108, 8, 55, 35, 4), // Grass
            new MatterProperties(MatterPhase.Static, 176, 0, 0, 196, 4, 64, 95, 8), // Wood
            new MatterProperties(MatterPhase.Granular, 158, 255, 255) // Ash
        };

        private readonly Cell[] cells;
        private readonly MatterProperties[] catalog;
        private readonly MatterLedger[] ledger = new MatterLedger[MatterCount];
        private readonly int[] nextThermal;
        private readonly uint[] revisions;
        private readonly byte[] awake;
        private readonly bool[] scheduled, changed, dirty;
        private readonly int seed;
        private bool stepping, thermalPresent;
        private int dirtyCount, burningCells;
        private int ignitions, extinguished, exhausted, fuelBurned, smokeProduced;

        public MatterWorld(int width, int height, int seed = 1, IReadOnlyDictionary<Matter, MatterProperties> settings = null)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
            ChunkColumns = (width - 1) / ChunkSize + 1;
            ChunkRows = (height - 1) / ChunkSize + 1;
            this.seed = seed;
            int count = checked(width * height);
            int chunks = checked(ChunkColumns * ChunkRows);
            cells = new Cell[count];
            nextThermal = new int[count];
            awake = new byte[chunks];
            scheduled = new bool[chunks];
            changed = new bool[chunks];
            dirty = new bool[chunks];
            revisions = new uint[chunks];
            catalog = (MatterProperties[])Defaults.Clone();
            if (settings != null)
                foreach (var entry in settings)
                {
                    int index = (int)entry.Key;
                    if (index > 0 && index < MatterCount && entry.Value.Phase == Defaults[index].Phase)
                        catalog[index] = entry.Value;
                }
            Metrics = new MatterMetrics(0, 0, count, 0, 0, 0);
        }

        public int Width { get; }
        public int Height { get; }
        public int ChunkColumns { get; }
        public int ChunkRows { get; }
        public int Tick { get; private set; }
        public MatterMetrics Metrics { get; private set; }
        public MatterReactions LastReactions { get; private set; }

        public static MatterProperties DefaultProperties(Matter matter) =>
            (int)matter < MatterCount ? Defaults[(int)matter] : default;

        public MatterLedger GetLedger(Matter matter) => (int)matter < MatterCount ? ledger[(int)matter] : default;

        public uint GetChunkRevision(int chunkX, int chunkY) => IsChunkInBounds(chunkX, chunkY)
            ? revisions[chunkY * ChunkColumns + chunkX] : 0;
        public bool IsChunkDirty(int chunkX, int chunkY) => IsChunkInBounds(chunkX, chunkY) &&
            dirty[chunkY * ChunkColumns + chunkX];

        public void ClearDirty()
        {
            Array.Clear(dirty, 0, dirty.Length);
            dirtyCount = 0;
        }

        public bool TrySnapshot(int x, int y, out MatterSample sample)
        {
            if (!IsInBounds(x, y))
            {
                sample = default;
                return false;
            }
            sample = Snapshot(cells[y * Width + x]);
            return true;
        }

        /// <summary>Copies a complete rectangle in ascending y and x order, or returns zero without touching the buffer.</summary>
        public int CopyRegion(int x, int y, int width, int height, MatterSample[] destination)
        {
            if (!IsInBounds(x, y) || width <= 0 || height <= 0 || width > Width - x || height > Height - y ||
                destination == null || destination.Length < width * height) return 0;
            int read = y * Width + x;
            int count = 0;
            for (int row = 0; row < height; row++, read += Width)
                for (int column = 0; column < width; column++)
                    destination[count++] = Snapshot(cells[read + column]);
            return count;
        }

        private MatterSample Snapshot(Cell cell)
        {
            int fuel = catalog[(int)cell.Kind].BurnRate > 0 ? cell.Mass : 0;
            return new MatterSample(cell.Kind, cell.Mass, cell.Heat, fuel, cell.Smoke, cell.IsBurning,
                (cell.Flags & FixedRock) != 0, (cell.Flags & TestBarrier) != 0);
        }

        public int Add(int x, int y, Matter kind, int amount = Capacity)
        {
            if (!IsInBounds(x, y) || kind == Matter.Empty || (int)kind >= MatterCount || amount <= 0) return 0;
            int index = y * Width + x;
            ref Cell cell = ref cells[index];
            if (cell.IsBlocked || (cell.Kind != Matter.Empty && cell.Kind != kind)) return 0;
            int admitted = Math.Min(Capacity - cell.Mass, amount);
            if (admitted == 0) return 0;
            cell.Kind = kind;
            cell.Mass += (byte)admitted;
            ledger[(int)kind].Injected += admitted;
            ledger[(int)kind].Current += admitted;
            Touch(x, y);
            return admitted;
        }

        public int Remove(int x, int y, int amount = Capacity)
        {
            if (!IsInBounds(x, y) || amount <= 0) return 0;
            ref Cell cell = ref cells[y * Width + x];
            int removed = Math.Min(amount, cell.Mass);
            if (removed == 0) return 0;
            ledger[(int)cell.Kind].Removed += removed;
            ledger[(int)cell.Kind].Current -= removed;
            cell.Mass -= (byte)removed;
            if (cell.Mass == 0) EmptyMatter(ref cell);
            Touch(x, y);
            return removed;
        }

        public int AddHeat(int x, int y, int amount)
        {
            if (!IsInBounds(x, y) || amount <= 0) return 0;
            ref Cell cell = ref cells[y * Width + x];
            if (cell.IsBlocked) return 0;
            int accepted = Math.Min(amount, MaxHeat - cell.Heat);
            if (accepted == 0) return 0;
            cell.Heat += (ushort)accepted;
            thermalPresent = true;
            Touch(x, y);
            return accepted;
        }

        public bool SetFixed(int x, int y, bool blocked) => SetBlock(x, y, blocked, FixedRock);
        public bool SetBarrier(int x, int y, bool blocked) => SetBlock(x, y, blocked, TestBarrier);

        private bool SetBlock(int x, int y, bool enabled, byte flag)
        {
            if (!IsInBounds(x, y)) return false;
            ref Cell cell = ref cells[y * Width + x];
            if (enabled && (cell.Mass > 0 || (flag == TestBarrier && (cell.Flags & FixedRock) != 0))) return false;
            byte next = enabled ? (byte)(cell.Flags | flag) : (byte)(cell.Flags & ~flag);
            if (next == cell.Flags) return true;
            cell.Flags = next;
            if (enabled)
            {
                cell.Heat = 0;
                cell.Smoke = 0;
            }
            Touch(x, y);
            return true;
        }

        public void ClearDynamic()
        {
            for (int chunk = 0; chunk < revisions.Length; chunk++) MarkDirty(chunk);
            for (int i = 0; i < cells.Length; i++)
                cells[i] = new Cell { Flags = (byte)(cells[i].Flags & FixedRock) };
            Array.Clear(ledger, 0, ledger.Length);
            Array.Clear(awake, 0, awake.Length);
            Array.Clear(changed, 0, changed.Length);
            Tick = 0;
            burningCells = 0;
            thermalPresent = false;
            LastReactions = default;
            Metrics = new MatterMetrics(0, 0, cells.Length, 0, dirtyCount, 0);
        }

        public void Step()
        {
            long start = Stopwatch.GetTimestamp();
            int activeChunks = 0, activeCells = 0;
            for (int chunk = 0; chunk < awake.Length; chunk++)
            {
                scheduled[chunk] = awake[chunk] > 0;
                if (!scheduled[chunk]) continue;
                awake[chunk]--;
                activeChunks++;
                activeCells += Math.Min(ChunkSize, Width - chunk % ChunkColumns * ChunkSize) *
                    Math.Min(ChunkSize, Height - chunk / ChunkColumns * ChunkSize);
            }

            Tick++;
            LastReactions = default;
            if (activeChunks > 0)
            {
                stepping = true;
                Gravity();
                Sweep(Stage.Liquid);
                if (catalog[(int)Matter.Water].LateralRate > 0) LevelSupportedWater();
                if (thermalPresent)
                {
                    burningCells = 0;
                    ignitions = extinguished = exhausted = fuelBurned = smokeProduced = 0;
                    Sweep(Stage.PrepareHeat);
                    thermalPresent = false;
                    Sweep(Stage.ApplyHeat);
                    if (thermalPresent)
                    {
                        Sweep(Stage.SmokeRise, down: true);
                        Sweep(Stage.SmokeSpread);
                    }
                    LastReactions = new MatterReactions(ignitions, extinguished, exhausted, fuelBurned, smokeProduced);
                }

                for (int chunk = 0; chunk < changed.Length; chunk++)
                {
                    if (!changed[chunk]) continue;
                    changed[chunk] = false;
                    MarkDirty(chunk);
                }
                stepping = false;
            }

            double milliseconds = (Stopwatch.GetTimestamp() - start) * (1000.0 / Stopwatch.Frequency);
            Metrics = new MatterMetrics(activeChunks, activeCells, cells.Length, burningCells, dirtyCount, milliseconds);
        }

        private bool IsInBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;
        private bool IsChunkInBounds(int x, int y) => (uint)x < (uint)ChunkColumns && (uint)y < (uint)ChunkRows;

        private void Touch(int x, int y)
        {
            int chunkX = x / ChunkSize, chunkY = y / ChunkSize;
            int chunk = chunkX + chunkY * ChunkColumns;
            awake[chunk] = QuietSteps;
            if (stepping) changed[chunk] = true;
            else MarkDirty(chunk);

            // Direct-neighbor wakeups are enough for pair phases. A diagonal granular fall
            // also needs the far corner when its source begins at a two-axis seam.
            int neighborX = x % ChunkSize == 0 ? -1 : x % ChunkSize == ChunkSize - 1 ? 1 : 0;
            int neighborY = y % ChunkSize == 0 ? -1 : y % ChunkSize == ChunkSize - 1 ? 1 : 0;
            if (neighborX != 0 && IsChunkInBounds(chunkX + neighborX, chunkY))
                awake[chunk + neighborX] = QuietSteps;
            if (neighborY != 0 && IsChunkInBounds(chunkX, chunkY + neighborY))
                awake[chunk + neighborY * ChunkColumns] = QuietSteps;
            if (neighborX != 0 && neighborY != 0 && IsChunkInBounds(chunkX + neighborX, chunkY + neighborY))
                awake[chunk + neighborY * ChunkColumns + neighborX] = QuietSteps;
        }

        private void MarkDirty(int chunk)
        {
            unchecked { revisions[chunk]++; }
            if (dirty[chunk]) return;
            dirty[chunk] = true;
            dirtyCount++;
        }

        private static void EmptyMatter(ref Cell cell)
        {
            cell.Kind = Matter.Empty;
            cell.Mass = 0;
            cell.Heat = 0;
            cell.AshResidue = 0;
            cell.Flags &= unchecked((byte)~(Flame | ColdContact));
        }
    }
}
