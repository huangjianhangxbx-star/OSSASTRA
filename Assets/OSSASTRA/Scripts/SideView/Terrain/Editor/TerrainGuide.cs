/* 规范化校准接点和贴附线，约束拖动边界供素材面板使用。 */
namespace Ossastra.SideView.TerrainWorkshop
{
    internal enum TerrainGuideHandle { Start, End, Anchor }

    internal readonly struct TerrainGuide
    {
        public readonly float Start, End, Anchor;

        public TerrainGuide(float start, float end, float anchor) =>
            (Start, End, Anchor) = (start, end, anchor);

        public TerrainGuide Move(TerrainGuideHandle handle, float x, float y, bool wall)
        {
            float along = wall ? 1 - y : x;
            float across = wall ? x : 1 - y;
            return handle switch
            {
                TerrainGuideHandle.Start => new TerrainGuide(Clamp(along, 0, End - .02f), End, Anchor),
                TerrainGuideHandle.End => new TerrainGuide(Start, Clamp(along, Start + .02f, 1), Anchor),
                _ => new TerrainGuide(Start, End, Clamp(across, 0, 1))
            };
        }

        private static float Clamp(float value, float minimum, float maximum) =>
            value < minimum ? minimum : value > maximum ? maximum : value;
    }
}
