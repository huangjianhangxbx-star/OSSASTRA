using System;

namespace Ossastra.OSPhysics
{
    public sealed partial class MatterWorld
    {
        private enum Stage { Granules, Downward, Liquid, PrepareHeat, ApplyHeat, SmokeRise, SmokeSpread }

        private const int WaterContactMass = 24;
        private const int WetFlag = 1 << 11;

        private void Gravity()
        {
            Sweep(Stage.Granules);
            Sweep(Stage.Downward);
        }

        private void Sweep(Stage stage, bool down = false)
        {
            bool reverse = (stage == Stage.Granules || stage == Stage.Downward) && ((Tick + seed) & 1) != 0;
            bool pairs = stage == Stage.Liquid || stage == Stage.SmokeSpread;
            int start = down ? Height - 1 : 0;
            int finish = down ? -1 : Height;
            int stride = down ? -1 : 1;
            for (int y = start; y != finish; y += stride)
            {
                int row = y * Width;
                int chunkRow = (y / ChunkSize) * ChunkColumns;
                for (int offset = 0; offset < ChunkColumns; offset++)
                {
                    int column = reverse ? ChunkColumns - 1 - offset : offset;
                    if (!scheduled[chunkRow + column]) continue;
                    int left = column * ChunkSize;
                    int right = Math.Min(Width, left + ChunkSize);
                    int dx = reverse ? -1 : pairs ? 2 : 1;
                    int xStart = reverse ? right - 1 : left + (pairs ? ((Tick + seed + y) & 1) : 0);
                    int xFinish = reverse ? left - 1 : right;
                    for (int x = xStart; reverse ? x > xFinish : x < xFinish; x += dx)
                    {
                        int cell = row + x;
                        switch (stage)
                        {
                            case Stage.Granules: FallGrain(cell, x, y, reverse); break;
                            case Stage.Downward: FallLiquid(cell, x, y); break;
                            case Stage.Liquid: SpreadLiquid(cell, x, y); break;
                            case Stage.PrepareHeat: PrepareHeat(cell, x, y); break;
                            case Stage.ApplyHeat: ApplyHeat(cell, x, y); break;
                            case Stage.SmokeRise: RiseSmoke(cell, x, y); break;
                            case Stage.SmokeSpread: SpreadSmoke(cell, x, y); break;
                        }
                    }
                }
            }
        }

        private void FallGrain(int i, int x, int y, bool reverse)
        {
            if (y == 0 || catalog[(int)cells[i].Kind].Phase != MatterPhase.Granular) return;
            int below = i - Width;
            if (CanDisplace(below))
            {
                MoveWhole(i, below, x, y, x, y - 1);
                return;
            }

            int first = reverse ? 1 : -1;
            if (!Slide(first)) Slide(-first);

            bool Slide(int direction)
            {
                int side = x + direction;
                if ((uint)side >= (uint)Width || cells[i + direction].IsBlocked ||
                    !CanDisplace(below + direction)) return false;
                MoveWhole(i, below + direction, x, y, side, y - 1);
                return true;
            }
        }

        private bool CanDisplace(int i)
        {
            ref Cell cell = ref cells[i];
            return !cell.IsBlocked && (cell.Kind == Matter.Empty || catalog[(int)cell.Kind].Phase == MatterPhase.Liquid);
        }

        private void FallLiquid(int i, int x, int y)
        {
            if (y == 0) return;
            ref Cell source = ref cells[i];
            var profile = catalog[(int)source.Kind];
            if (profile.Phase != MatterPhase.Liquid) return;
            int below = i - Width;
            ref Cell target = ref cells[below];
            if (target.IsBlocked) return;
            if (target.Kind == Matter.Empty || target.Kind == source.Kind)
            {
                int flow = Math.Min(profile.DownwardRate, Math.Min(source.Mass, Capacity - target.Mass));
                Transfer(i, below, flow, x, y, x, y - 1);
            }
            else
            {
                var underneath = catalog[(int)target.Kind];
                if (underneath.Phase == MatterPhase.Liquid && profile.Density > underneath.Density)
                {
                    MarkColdContact(source, ref target);
                    MoveWhole(i, below, x, y, x, y - 1);
                }
            }
        }

        private void SpreadLiquid(int i, int x, int y)
        {
            if (x == Width - 1) return;
            ref Cell a = ref cells[i];
            ref Cell b = ref cells[i + 1];
            if (a.IsBlocked || b.IsBlocked) return;
            var profileA = catalog[(int)a.Kind];
            var profileB = catalog[(int)b.Kind];
            bool liquidA = profileA.Phase == MatterPhase.Liquid;
            bool liquidB = profileB.Phase == MatterPhase.Liquid;
            if (!liquidA && !liquidB) return;

            if (a.Kind == b.Kind || a.Kind == Matter.Empty || b.Kind == Matter.Empty)
            {
                int difference = a.Mass - b.Mass;
                if (difference >= 2)
                    Transfer(i, i + 1, Math.Min(profileA.LateralRate, difference / 2), x, y, x + 1, y);
                else if (difference <= -2)
                    Transfer(i + 1, i, Math.Min(profileB.LateralRate, -difference / 2), x + 1, y, x, y);
                return;
            }

            // A dense liquid can enter the side of a lighter pool. Lift that oil only
            // where there is room above and the denser source has no lower path.
            if (!liquidA || !liquidB || y == 0 || y + 1 >= Height || profileA.Density == profileB.Density) return;
            bool leftDense = profileA.Density > profileB.Density;
            int heavy = leftDense ? i : i + 1;
            int light = leftDense ? i + 1 : i;
            int heavyX = leftDense ? x : x + 1, lightX = leftDense ? x + 1 : x;
            int lifted = light + Width;
            Matter denseKind = cells[heavy].Kind;
            if (!cells[lifted].IsBlocked && cells[lifted].Kind == Matter.Empty && cells[heavy].Mass >= 64 &&
                cells[light - Width].Kind == denseKind &&
                (cells[heavy - Width].IsBlocked || cells[heavy - Width].Kind == denseKind))
            {
                MarkColdContact(cells[heavy], ref cells[light]);
                MoveWhole(light, lifted, lightX, y, lightX, y + 1);
                Transfer(heavy, light, Math.Min(catalog[(int)denseKind].LateralRate, cells[heavy].Mass),
                    heavyX, y, lightX, y);
            }
        }

        private static void MarkColdContact(Cell cooling, ref Cell heated)
        {
            // Keep contact through a density swap. The formerly low oil may already be above
            // the poured water by the reaction phase, so positions alone would miss the quench.
            if (cooling.Kind == Matter.Water && cooling.Mass >= WaterContactMass && heated.Heat != 0)
                heated.Flags |= ColdContact;
        }

        private void LevelSupportedWater()
        {
            // Pair exchanges move the front. Equalization redistributes only inside a
            // continuous supported run, where adjacent cells share the same water surface.
            // This removes persistent one-unit gradients without moving water over a barrier.
            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                int chunkRow = (y / ChunkSize) * ChunkColumns;
                for (int x = 0; x < Width;)
                {
                    if (!scheduled[chunkRow + x / ChunkSize])
                    {
                        x = Math.Min(Width, (x / ChunkSize + 1) * ChunkSize);
                        continue;
                    }
                    if (!SupportedWater(row + x, y))
                    {
                        x++;
                        continue;
                    }

                    int left = x;
                    while (left > 0 && SupportedWater(row + left - 1, y)) left--;
                    int total = 0;
                    long heatSum = 0;
                    for (x = left; x < Width && SupportedWater(row + x, y); x++)
                    {
                        var c = cells[row + x];
                        total += c.Mass;
                        heatSum += c.Mass * c.Heat;
                    }

                    int length = x - left;
                    if (length < 2) continue;
                    int fill = total / length, extra = total % length;
                    int heat = (int)(heatSum / total);
                    // Distribute rounding residual spatially and identically on subsequent ticks.
                    int remainder = length / 2;
                    for (int column = left; column < x; column++)
                    {
                        int desired = fill;
                        remainder += extra;
                        if (remainder >= length)
                        {
                            remainder -= length;
                            desired++;
                        }
                        ref Cell cell = ref cells[row + column];
                        if (cell.Mass != desired || cell.Heat != heat)
                        {
                            cell.Mass = (byte)desired;
                            cell.Heat = (ushort)heat;
                            Touch(column, y);
                        }
                    }
                }
            }
        }

        private bool SupportedWater(int index, int y)
        {
            if (cells[index].Kind != Matter.Water) return false;
            if (y == 0) return true;
            ref Cell below = ref cells[index - Width];
            return below.IsBlocked || (below.Kind != Matter.Empty && below.Kind != Matter.Oil &&
                (below.Kind != Matter.Water || below.Mass == Capacity));
        }

        private void Transfer(int from, int to, int amount, int x, int y, int tx, int ty)
        {
            if (amount == 0) return;
            ref Cell source = ref cells[from];
            ref Cell target = ref cells[to];
            int targetMass = target.Mass + amount;
            target.Heat = (ushort)((target.Heat * target.Mass + source.Heat * amount) / targetMass);
            if (source.AshResidue != 0)
            {
                int residue = source.AshResidue * amount / source.Mass;
                target.AshResidue = (ushort)Math.Min(Capacity * Capacity, target.AshResidue + residue);
                source.AshResidue -= (ushort)residue;
            }
            if (amount == source.Mass) target.Flags |= (byte)(source.Flags & (Flame | ColdContact));
            target.Kind = source.Kind;
            target.Mass = (byte)targetMass;
            source.Mass -= (byte)amount;
            if (source.Mass == 0) EmptyMatter(ref source);
            Touch(x, y);
            Touch(tx, ty);
        }

        private void MoveWhole(int from, int to, int x, int y, int tx, int ty)
        {
            Cell source = cells[from], target = cells[to];
            // Smoke belongs to the space. Fuel, heat and moisture move with the matter.
            (source.Smoke, target.Smoke) = (target.Smoke, source.Smoke);
            cells[from] = target;
            cells[to] = source;
            Touch(x, y);
            Touch(tx, ty);
        }

        private void PrepareHeat(int i, int x, int y)
        {
            ref Cell cell = ref cells[i];
            var rule = catalog[(int)cell.Kind];
            if (cell.IsBlocked || (rule.BurnRate == 0 && cell.Heat == 0))
            {
                nextThermal[i] = 0;
                return;
            }

            bool wet = cell.Kind == Matter.Water || IsQuenched(i, x, y);
            int cooling = cell.Kind == Matter.Empty ? 38 : 9;
            int heat = wet ? 0 : Math.Max(0, cell.Heat - cooling);
            if (rule.BurnRate > 0 && !wet)
            {
                if (cell.IsBurning) heat = Math.Max(heat, rule.IgnitionHeat + 32);
                if (x > 0) heat += NeighborHeat(i - 1, x - 1, y);
                if (x + 1 < Width) heat += NeighborHeat(i + 1, x + 1, y);
                if (y > 0) heat += NeighborHeat(i - Width, x, y - 1);
                if (y + 1 < Height) heat += NeighborHeat(i + Width, x, y + 1);
            }
            nextThermal[i] = Math.Min(MaxHeat, heat) | (wet ? WetFlag : 0);
        }

        private int NeighborHeat(int i, int x, int y)
        {
            ref Cell neighbor = ref cells[i];
            return neighbor.IsBurning && !IsQuenched(i, x, y) ? catalog[(int)neighbor.Kind].HeatSpread : 0;
        }

        private bool IsQuenched(int i, int x, int y)
        {
            if ((cells[i].Flags & ColdContact) != 0) return true;
            if (ledger[(int)Matter.Water].Current == 0) return false;
            if (y + 1 < Height && IsWetWater(i + Width)) return true;
            // Oil in a stratified pool burns above its supporting water.
            return (cells[i].Kind != Matter.Oil && y > 0 && IsWetWater(i - Width)) ||
                (x > 0 && IsWetWater(i - 1)) || (x + 1 < Width && IsWetWater(i + 1));
        }

        private bool IsWetWater(int i) => cells[i].Kind == Matter.Water && cells[i].Mass >= WaterContactMass;

        private void ApplyHeat(int i, int x, int y)
        {
            ref Cell cell = ref cells[i];
            if (cell.IsBlocked) return;
            int thermal = nextThermal[i];
            if (thermal == 0 && cell.Heat == 0 && !cell.IsBurning)
            {
                thermalPresent |= cell.Smoke > 0;
                return;
            }
            var rule = catalog[(int)cell.Kind];
            bool wasBurning = cell.IsBurning;
            bool quenched = (thermal & WetFlag) != 0;
            int heat = thermal & MaxHeat;
            bool burning = !quenched && rule.BurnRate > 0 && heat >= Math.Max(1, rule.IgnitionHeat);
            if (wasBurning && !burning) extinguished++;
            if (!wasBurning && burning) ignitions++;
            bool different = cell.Heat != heat || wasBurning != burning;
            cell.Heat = (ushort)heat;
            cell.Flags &= unchecked((byte)~ColdContact);
            if (burning)
            {
                cell.Flags |= Flame;
                int consumed = Math.Min(cell.Mass, rule.BurnRate);
                ledger[(int)cell.Kind].Reacted += consumed;
                ledger[(int)cell.Kind].Current -= consumed;
                fuelBurned += consumed;
                cell.Mass -= (byte)consumed;
                cell.AshResidue = (ushort)Math.Min(Capacity * Capacity, cell.AshResidue + consumed * rule.AshYield);
                int smoke = Math.Min(Capacity - cell.Smoke, consumed * rule.SmokePerFuel);
                cell.Smoke += (byte)smoke;
                smokeProduced += smoke;
                if (cell.Mass == 0)
                {
                    int ash = cell.AshResidue / Capacity;
                    EmptyMatter(ref cell);
                    if (ash > 0)
                    {
                        ledger[(int)Matter.Ash].Produced += ash;
                        ledger[(int)Matter.Ash].Current += ash;
                        cell.Kind = Matter.Ash;
                        cell.Mass = (byte)ash;
                    }
                    exhausted++;
                }
                else burningCells++;
            }
            else cell.Flags &= unchecked((byte)~Flame);

            thermalPresent |= cell.Heat > 0 || cell.Smoke > 0 || cell.IsBurning;
            if (different || burning) Touch(x, y);
        }

        private void RiseSmoke(int i, int x, int y)
        {
            ref Cell source = ref cells[i];
            if (source.Smoke == 0) return;
            source.Smoke -= 1;
            if (y + 1 < Height)
            {
                ref Cell above = ref cells[i + Width];
                if (GasCanEnter(above))
                {
                    int rise = Math.Min(Capacity - above.Smoke, (source.Smoke + 1) / 2);
                    if (rise > 0)
                    {
                        above.Smoke += (byte)rise;
                        source.Smoke -= (byte)rise;
                        Touch(x, y + 1);
                    }
                }
            }
            Touch(x, y);
        }

        private void SpreadSmoke(int i, int x, int y)
        {
            if (x == Width - 1) return;
            ref Cell a = ref cells[i];
            ref Cell b = ref cells[i + 1];
            int delta = a.Smoke - b.Smoke;
            if (delta >= 4 && GasCanEnter(b) || delta <= -4 && GasCanEnter(a))
            {
                int spread = delta / 4;
                a.Smoke = (byte)(a.Smoke - spread);
                b.Smoke = (byte)(b.Smoke + spread);
                Touch(x, y);
                Touch(x + 1, y);
            }
        }

        private bool GasCanEnter(Cell cell) => !cell.IsBlocked && (cell.Mass < 16 ||
            (catalog[(int)cell.Kind].Phase == MatterPhase.Liquid && cell.Mass < 48));
    }
}
