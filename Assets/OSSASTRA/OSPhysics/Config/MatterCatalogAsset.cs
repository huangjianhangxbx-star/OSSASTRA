using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.OSPhysics
{
    [CreateAssetMenu(fileName = "MatterCatalog_SAMPLE", menuName = "OSSASTRA/OSPhysics/实验物质目录")]
    public sealed class MatterCatalogAsset : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            public Matter kind;
            public MatterPhase phase;
            [Min(0)] public int density;
            [Range(0, 255)] public int downwardRate;
            [Range(0, 255)] public int lateralRate;
            [Range(0, 1023)] public int ignitionHeat;
            [Range(0, 255)] public int burnRate;
            [Range(0, 1023)] public int heatSpread;
            [Range(0, 255)] public int ashYield;
            [Range(0, 255)] public int smokePerFuel;

            public MatterProperties ToSettings()
            {
                return new MatterProperties(phase, density, downwardRate, lateralRate,
                    ignitionHeat, burnRate, heatSpread, ashYield, smokePerFuel);
            }
        }

        [Tooltip("实验值。液量最大 255；温度最大 1023；流动和燃耗按每模拟步计。")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public IReadOnlyDictionary<Matter, MatterProperties> BuildSettings()
        {
            var settings = new Dictionary<Matter, MatterProperties>(entries.Length);
            foreach (var entry in entries)
            {
                if (entry == null || entry.kind == Matter.Empty)
                    throw new InvalidOperationException("物质目录含空条目，请先修正实验配置。");
                if (settings.ContainsKey(entry.kind))
                    throw new InvalidOperationException($"物质目录重复定义 {entry.kind}。");
                settings.Add(entry.kind, entry.ToSettings());
            }
            return settings;
        }

        public void PopulateSample()
        {
            var kinds = new[] { Matter.Water, Matter.Oil, Matter.Sand, Matter.Grass, Matter.Wood, Matter.Ash };
            entries = new Entry[kinds.Length];
            for (int index = 0; index < kinds.Length; index++)
            {
                var kind = kinds[index];
                var defaults = MatterWorld.DefaultProperties(kind);
                entries[index] = new Entry
                {
                    kind = kind,
                    phase = defaults.Phase,
                    density = defaults.Density,
                    downwardRate = defaults.DownwardRate,
                    lateralRate = defaults.LateralRate,
                    ignitionHeat = defaults.IgnitionHeat,
                    burnRate = defaults.BurnRate,
                    heatSpread = defaults.HeatSpread,
                    ashYield = defaults.AshYield,
                    smokePerFuel = defaults.SmokePerFuel
                };
            }
        }
    }
}
