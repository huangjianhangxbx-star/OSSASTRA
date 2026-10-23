#if UNITY_EDITOR
using System;

public static class GalAutoAdvanceTimingProbe
{
    public static string Run()
    {
        AssertApproximately(1.8f, GalAutoAdvanceTiming.Calculate("", 0f));
        AssertApproximately(2.2f, GalAutoAdvanceTiming.Calculate("abcdefghij", 0f));
        AssertApproximately(
            2.06f,
            GalAutoAdvanceTiming.Calculate("<b>abc de fghi</b>", 0f));
        AssertApproximately(
            6.5f,
            GalAutoAdvanceTiming.Calculate(new string('字', 100), 0f));
        AssertApproximately(
            0.35f,
            GalAutoAdvanceTiming.Calculate("ignored", 0.35f));
        return "PASS gal auto advance timing";
    }

    private static void AssertApproximately(float expected, float actual)
    {
        if (float.IsNaN(actual) ||
            float.IsInfinity(actual) ||
            Math.Abs(expected - actual) > 0.0001f)
        {
            throw new Exception(
                $"Expected approximately {expected}, actual {actual}");
        }
    }
}
#endif
