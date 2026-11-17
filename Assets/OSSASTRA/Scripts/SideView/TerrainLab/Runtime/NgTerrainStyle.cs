using System;
using UnityEngine;

namespace Ossastra.SideView.TerrainLab
{
    [CreateAssetMenu(menuName = "OSSASTRA/Terrain Lab/Material Set")]
    public sealed class NgTerrainStyle : ScriptableObject
    {
        [Serializable]
        public sealed class Sample
        {
            public Material material;
            public RectInt pixels;
            public Rect destinationCells = new Rect(0, 0, 1, 1);

            public bool TryGetUv(out Rect uv)
            {
                uv = default;
                var texture = material != null ? material.mainTexture : null;
                if (texture == null || pixels.xMin < 0 || pixels.yMin < 0 || pixels.xMax > texture.width ||
                    pixels.yMax > texture.height || pixels.width < 1 || pixels.height < 1) return false;
                uv = new Rect((float)pixels.x / texture.width, (float)pixels.y / texture.height,
                    (float)pixels.width / texture.width, (float)pixels.height / texture.height);
                return true;
            }
        }

        [Serializable]
        public sealed class Rule
        {
            public TerrainNeighbors require;
            public TerrainNeighbors forbid;
            public Sample[] variants = Array.Empty<Sample>();

            public bool Matches(TerrainNeighbors neighbors) => (neighbors & require) == require && (neighbors & forbid) == 0;
        }

        [Serializable]
        public sealed class Layer
        {
            public string surfaceKind = "Ground";
            public int sortingOrder = -10;
            public float depth = .03f;
            public bool required;
            public Rule[] rules = Array.Empty<Rule>();

            public Sample Choose(TerrainCell cell, int material, TerrainNeighbors neighbors)
            {
                for (int i = 0; i < rules.Length; i++)
                {
                    var rule = rules[i];
                    if (rule == null || !rule.Matches(neighbors) || rule.variants.Length == 0) continue;
                    return rule.variants[NgTerrainGrid.Variant(cell, material, rule.variants.Length)];
                }
                return null;
            }
        }

        public Layer[] layers = Array.Empty<Layer>();
        public Color guideColor = new Color(.35f, .76f, .54f, .68f);
    }
}
