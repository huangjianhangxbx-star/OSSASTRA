using System;
using UnityEngine;

namespace Ossastra.WorldRendering.SideView
{
    public sealed class SideViewAceOriginalReferenceData : ScriptableObject
    {
        [Serializable]
        public struct Palette
        {
            public string name;
            public float ambientScale, emissionIntensity;
            public Color emission, emissionX, emissionY, emissionZ, emissionA;
        }

        [Serializable]
        public struct Page
        {
            public int index;
            public Texture2D[] main;
            public Texture2D extra, extra2;
        }

        [Serializable]
        public struct Frame
        {
            public int mesh, page, endTick;
        }

        [Serializable]
        public struct Clip
        {
            public string name, label;
            public bool loop;
            public int totalTicks;
            public Frame[] frames;

            public int FrameAt(int tick)
            {
                int first = 0, last = frames.Length - 1;
                while (first < last)
                {
                    int middle = (first + last) / 2;
                    if (frames[middle].endTick < tick) first = middle + 1;
                    else last = middle;
                }
                return first;
            }
        }

        [SerializeField] private int ticksPerSecond = 60;
        [SerializeField] private Palette[] palettes = Array.Empty<Palette>();
        [SerializeField] private Page[] pages = Array.Empty<Page>();
        [SerializeField] private Clip[] clips = Array.Empty<Clip>();
        [SerializeField] private Mesh[] meshes = Array.Empty<Mesh>();

        public int TicksPerSecond => ticksPerSecond;
        public int PaletteCount => palettes.Length;
        public int ClipCount => clips.Length;
        internal Palette PaletteAt(int index) => palettes[index];
        internal Clip ClipAt(int index) => clips[index];
        internal Mesh MeshAt(int index) => meshes[index];

        internal Page PageAt(int sourceIndex)
        {
            foreach (var page in pages)
                if (page.index == sourceIndex) return page;
            throw new InvalidOperationException("Missing source texture page " + sourceIndex);
        }

        public void Configure(int sourceTicksPerSecond, Palette[] sourcePalettes, Page[] sourcePages,
            Clip[] sourceClips, Mesh[] sourceMeshes)
        {
            ticksPerSecond = sourceTicksPerSecond;
            palettes = sourcePalettes;
            pages = sourcePages;
            clips = sourceClips;
            meshes = sourceMeshes;
        }
    }
}
