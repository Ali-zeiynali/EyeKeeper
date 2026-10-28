using System;

namespace EyeKeeper;

public static class FluidStyles
{
    public static readonly string[] Styles = ["Silk", "Marble", "Ribbons", "Vortex", "Bloom", "Prism", "Aurora", "Contour"];
    public static readonly string[] Names = ["Random", .. Styles];
    public static int Choose(string name, int previous = -1)
    {
        int i = Array.IndexOf(Styles, name);
        if (i >= 0) return i;
        if (previous < 0 || previous >= Styles.Length) return Random.Shared.Next(Styles.Length);
        i = Random.Shared.Next(Styles.Length - 1); return i >= previous ? i + 1 : i;
    }
}
