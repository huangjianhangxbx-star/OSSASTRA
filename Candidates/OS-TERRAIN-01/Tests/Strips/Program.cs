using System;
using System.Linq;
using Ossastra.SideView.TerrainWorkshop;

static void Check(bool okay, string reason)
{
    if (!okay) throw new Exception(reason);
}

var strip = TerrainStripPacking.Place(0, 5, 2, 2, false).ToArray();
Check(strip.Length == 3 && strip[2].Low == 4 && strip[2].High == 5 && strip[2].Origin == 4, "short repeat clips with unchanged source scale");
var extended = TerrainStripPacking.Place(-3, 5, 2, 2, false).ToArray();
Check(extended.Where(slice => slice.Origin >= 0).SequenceEqual(strip), "extending a run does not rephase existing pieces");
var whole = TerrainStripPacking.Place(.3f, 2.4f, 2, 2, true).ToArray();
Check(whole.Length == 1 && whole[0].Low == .3f && Math.Abs(whole[0].High - 2.3f) < .001f, "whole art falls back to one true-length placement");
Check(!TerrainStripPacking.Place(0, 1, 2, 2, true).Any(), "short whole piece requires a cap or warning");
Check(!TerrainStripPacking.Place(0, 10, 0, 1, false).Any(), "invalid span produces no geometry");
Console.WriteLine("5 terrain strip packing contracts passed.");
