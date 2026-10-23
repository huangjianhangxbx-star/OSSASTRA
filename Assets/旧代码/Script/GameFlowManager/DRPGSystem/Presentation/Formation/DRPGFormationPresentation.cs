using UnityEngine;

public static class DRPGFormationColorPalette
{
    public static readonly Color32 Front = new Color32(235, 55, 72, 255);
    public static readonly Color32 Middle = new Color32(242, 185, 54, 255);
    public static readonly Color32 Rear = new Color32(57, 142, 235, 255);

    public static Color Resolve(DRPGFormationRow row, float alpha = 1f)
    {
        Color color = row == DRPGFormationRow.Front
            ? Front
            : row == DRPGFormationRow.Middle ? Middle : Rear;
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}

public static class DRPGFormationWorldPresentation
{
    public static float ResolveScale(DRPGFormationRow row)
    {
        return row == DRPGFormationRow.Front
            ? 1f
            : row == DRPGFormationRow.Middle ? 0.88f : 0.76f;
    }

    public static int ResolveSortingBase(DRPGFormationRow row) =>
        300 - (int)row * 100;
}
