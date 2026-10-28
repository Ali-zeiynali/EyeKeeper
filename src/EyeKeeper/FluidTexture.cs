using System;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EyeKeeper;

// A domain-warped color field: real moving folds, rather than translated blurred circles.
// Pure CPU rasterization also works in offscreen renders and on machines without a shader-capable GPU.
public static class FluidTexture
{
    const int TableSize = 16384;
    static readonly float[] Sines = MakeSines();
    static float[] MakeSines()
    {
        var table = new float[TableSize];
        for (int i = 0; i < table.Length; i++) table[i] = MathF.Sin(i * MathF.Tau / TableSize);
        return table;
    }
    static float Sin(float value)
    {
        float n = value * (TableSize / MathF.Tau);
        int i = (int)MathF.Floor(n); float f = n - i;
        float a = Sines[i & (TableSize - 1)], b = Sines[(i + 1) & (TableSize - 1)];
        return a + (b - a) * f;
    }
    static float Ridge(float value, float center, float width)
    {
        float r = MathF.Max(0, 1 - MathF.Abs(value - center) * width);
        return r * r * r;
    }
    static (float r, float g, float b) Parse(string hex)
    {
        uint n = Convert.ToUInt32(hex.AsSpan(1).ToString(), 16);
        return ((n >> 16) & 255, (n >> 8) & 255, n & 255);
    }
    public static int[] Pixels(int palette, double seconds, int width, int height, int mode = 0)
    {
        var colors = Palettes.Colors[Math.Clamp(palette, 0, Palettes.Colors.Length - 1)];
        var a = Parse(colors[0]); var b = Parse(colors[1]); var c = Parse(colors[2]); var d = Parse(colors[3]);
        // Closing the color ramp makes every fold cycle through shadow, saturated body and light.
        var ramp = new[] { a, b, c, d, c, b, a };
        var lut = new (float r, float g, float b)[1024];
        for (int i = 0; i < lut.Length; i++)
        {
            float s = i / 1023f * 6; int k = Math.Min(5, (int)s); float f = s - k;
            f = f * f * (3 - 2 * f);
            lut[i] = (ramp[k].r + (ramp[k + 1].r - ramp[k].r) * f,
                ramp[k].g + (ramp[k + 1].g - ramp[k].g) * f, ramp[k].b + (ramp[k + 1].b - ramp[k].b) * f);
        }
        var pixels = new int[checked(width * height)];
        float t = (float)(seconds % 10000) * .22f;
        float aspect = Math.Clamp(width / (float)height, .7f, 3.8f);
        Parallel.For(0, height, new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 1, 4) }, y =>
        {
            float py = (y / (float)(height - 1) - .5f) * 3.4f;
            for (int x = 0; x < width; x++)
            {
                float px = (x / (float)(width - 1) - .5f) * 3.4f * aspect;
                // Two nested curl layers form broad ribbons with smaller flowing creases.
                float qx = Sin(px * 1.1f + Sin(py * 1.3f + t));
                float qy = Sin(py * 1.0f + Sin(px * 1.25f - t * .8f) + 1.6f);
                float wx = px + .95f * qx + .40f * Sin(py * 1.9f - t * .75f);
                float wy = py + .95f * qy + .40f * Sin(px * 1.7f + t * .6f);
                float rx = Sin(wx * 1.65f + wy * .65f + t * .6f);
                float ry = Sin(wy * 1.5f - wx * .55f - t * .7f);
                float phase = (wx + .68f * rx) * 1.55f + (wy + .68f * ry) * 1.15f + t * .35f;
                float field = .5f + .5f * Sin(phase);
                switch (mode)
                {
                    case 1: field = .5f + .5f * Sin(phase * 2.1f + .8f * Sin(wx * 2.3f - wy * 1.4f + t)); break;
                    case 2:
                        float wave = wy + .8f * Sin(wx * 1.1f + t * .7f) + .38f * Sin(wx * 2.3f - t * .5f);
                        field = MathF.Exp(-wave * wave * 1.4f); break;
                    case 3: field = .5f + .5f * Sin(MathF.Atan2(wy, wx) * 3 + MathF.Sqrt(wx * wx + wy * wy) * 3.7f - t); break;
                    case 4: field = .5f + .5f * Sin(wx * .6f + wy * .7f + Sin(wy * .8f + t)); break;
                    case 5: field = .5f + .5f * Sin(phase * 1.3f + Sin(wx * 1.1f - wy * 1.4f)); break;
                    case 6: field = .5f + .5f * Sin(py * 1.3f + Sin(wx * .9f + t) * 1.6f + wy * .4f); break;
                    case 7: field = .5f + .5f * Sin(phase * 3.5f + .4f * Sin(wy * 3 + t)); break;
                }
                var color = lut[Math.Clamp((int)(field * 1023), 0, 1023)];
                if (mode == 4)
                {
                    float blend = (.5f + .5f * Sin(wx * .6f - wy * .5f - t * .4f)) * .75f;
                    color = ((b.r + (c.r - b.r) * field) * (1 - blend) + (d.r + (a.r - d.r) * field) * blend,
                        (b.g + (c.g - b.g) * field) * (1 - blend) + (d.g + (a.g - d.g) * field) * blend,
                        (b.b + (c.b - b.b) * field) * (1 - blend) + (d.b + (a.b - d.b) * field) * blend);
                }
                if (mode == 5)
                {
                    var other = lut[Math.Clamp((int)((.5f + .5f * Sin(phase + 1.5f + wy * .5f)) * 1023), 0, 1023)];
                    color = (color.r * .72f + other.r * .28f, color.g * .72f + other.g * .28f, color.b * .72f + other.b * .28f);
                }
                float light = .83f + .17f * Sin(wx * .7f - wy * .8f + t * .35f);
                float shine = Ridge(field, .62f, 19) * .20f + Ridge(field, .245f, 35) * .085f;
                if (mode == 7) shine += Ridge(field, .82f, 30) * .28f;
                int red = Math.Clamp((int)(color.r * light + (255 - color.r * light) * shine), 0, 255);
                int green = Math.Clamp((int)(color.g * light + (255 - color.g * light) * shine), 0, 255);
                int blue = Math.Clamp((int)(color.b * light + (255 - color.b * light) * shine), 0, 255);
                pixels[y * width + x] = unchecked((int)0xFF000000) | red << 16 | green << 8 | blue;
            }
        });
        return pixels;
    }
    public static BitmapSource Create(int palette, double seconds, int width = 768, int height = 480, int mode = 0)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, Pixels(palette, seconds, width, height, mode), width * 4);
        bitmap.Freeze(); return bitmap;
    }
}
