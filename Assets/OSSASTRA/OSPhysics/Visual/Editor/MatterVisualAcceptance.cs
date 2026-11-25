using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ossastra.OSPhysics.Editor
{
    public static class MatterVisualAcceptance
    {
        public static string Verify()
        {
            var world = new MatterWorld(64, 32);
            world.SetFixed(0, 0, true);
            world.Add(15, 6, Matter.Water, 174);
            world.Add(48, 9, Matter.Wood);
            var surface = new GameObject("Matter Visual Acceptance");
            try
            {
                var visual = surface.AddComponent<MatterVisual>();
                visual.Bind(world, new Vector2(-3.2f, -1.6f), .1f);
                Require(visual.LastUploadedChunks == 2, "Every initial chunk was expected to reach the new texture.");
                ulong fingerprint = Fingerprint(world);
                foreach (MatterDisplay mode in Enum.GetValues(typeof(MatterDisplay)))
                {
                    visual.Refresh(mode, .2f);
                    Require(visual.LastUploadedChunks == 0, "Switching views should reuse the same snapshot texture.");
                    Require(Fingerprint(world) == fingerprint, "A visual mode changed the simulation snapshot.");
                }

                world.Add(34, 12, Matter.Oil, 85);
                visual.Refresh(MatterDisplay.Natural, .2f);
                Require(visual.LastUploadedChunks == 1, "One brush edit should upload only its chunk.");
                Require(visual.GetComponent<MeshFilter>().sharedMesh.vertexCount == 4,
                    "The snapshot should render on one quad without per-cell render objects.");
                return "OSPhysics visual: four modes on one snapshot, 2 initial uploads, 0 on view switches, 1 on a localized edit";
            }
            finally
            {
                if (Application.isPlaying) Object.Destroy(surface);
                else Object.DestroyImmediate(surface);
            }
        }

        public static ulong Fingerprint(MatterWorld world)
        {
            ulong hash = 14695981039346656037UL;
            for (int y = 0; y < world.Height; y++)
                for (int x = 0; x < world.Width; x++)
                {
                    world.TrySnapshot(x, y, out var cell);
                    Mix(ref hash, (uint)cell.Kind);
                    Mix(ref hash, (uint)cell.Amount);
                    Mix(ref hash, (uint)cell.Heat);
                    Mix(ref hash, (uint)cell.Fuel);
                    Mix(ref hash, (uint)cell.Smoke);
                    Mix(ref hash, cell.Fixed ? 1u : 0u);
                    Mix(ref hash, cell.Barrier ? 1u : 0u);
                    Mix(ref hash, cell.Burning ? 1u : 0u);
                }
            return hash;
        }

        private static void Mix(ref ulong hash, uint value) => hash = unchecked((hash ^ value) * 1099511628211UL);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
