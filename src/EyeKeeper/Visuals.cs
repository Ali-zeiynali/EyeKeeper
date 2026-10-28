using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;

namespace EyeKeeper;

public static class Art
{
    static readonly Dictionary<string, SolidColorBrush> BrushesCache = [];
    static readonly Dictionary<(string, double, Color, bool, bool), FormattedText> TextCache = [];
    public static SolidColorBrush Brush(string hex)
    {
        if (BrushesCache.TryGetValue(hex, out var found)) return found;
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); brush.Freeze();
        BrushesCache[hex] = brush; return brush;
    }
    public static void Text(DrawingContext dc, string value, double size, Brush color, double x, double y,
        bool bold = false, bool center = false, bool rtl = false)
    {
        var key = (value, size, color is SolidColorBrush solid ? solid.Color : Colors.White, bold, rtl);
        if (!TextCache.TryGetValue(key, out var text))
        {
            text = new FormattedText(value, CultureInfo.InvariantCulture,
            rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
                size, color, 1);
            TextCache[key] = text;
        }
        // RTL formatted text extends to the left of its origin in WPF.
        double drawX = center ? x + (rtl ? text.Width / 2 : -text.Width / 2) : x + (rtl ? text.Width : 0);
        dc.DrawText(text, new Point(drawX, y));
    }
    public static void Logo(DrawingContext dc, double x, double y, double size, bool tile = false)
    {
        dc.PushTransform(new TranslateTransform(x, y));
        dc.PushTransform(new ScaleTransform(size / 64, size / 64));
        if (tile) dc.DrawRoundedRectangle(new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#263C54"), (Color)ColorConverter.ConvertFromString("#111B2D"), 65), null, new Rect(0, 0, 64, 64), 18, 18);
        var eye = Geometry.Parse("M 9,32 C 20,13 44,13 55,32 C 44,51 20,51 9,32 Z");
        var pen = new Pen(Brush("#B9F2E2"), 3.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        dc.DrawGeometry(null, pen, eye);
        dc.DrawEllipse(Brush("#B9F2E2"), null, new Point(32, 32), 8, 8);
        dc.DrawEllipse(Brush("#F1FFF9"), null, new Point(35, 29), 2.5, 2.5);
        dc.Pop(); dc.Pop();
    }
    public static void Ring(DrawingContext dc, Point center, double radius, double progress, double thickness, Brush foreground)
    {
        dc.DrawEllipse(null, new Pen(Brush("#28FFFFFF"), thickness), center, radius, radius);
        progress = Math.Clamp(progress, .0001, .9999);
        double angle = progress * Math.PI * 2;
        var path = new StreamGeometry();
        using (var ctx = path.Open())
        {
            ctx.BeginFigure(new Point(center.X, center.Y - radius), false, false);
            ctx.ArcTo(new Point(center.X + Math.Sin(angle) * radius, center.Y - Math.Cos(angle) * radius),
                new Size(radius, radius), 0, angle > Math.PI, SweepDirection.Clockwise, true, false);
        }
        dc.DrawGeometry(null, new Pen(foreground, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, path);
    }
}

// Shared by real overlays and offscreen previews. The preview never creates a native window.
public sealed class RestVisual : FrameworkElement
{
    readonly VisualCollection layers;
    readonly ContainerVisual materialClip = new();
    readonly DrawingVisual material = new();
    readonly DrawingVisual ink = new();
    FluidEffect? effect;
    Rect lastScene;
    public bool HardwareAccelerated => effect != null;
    public int MotionStyle { get; set; }
    public RestVisual()
    {
        materialClip.Children.Add(material);
        layers = new VisualCollection(this) { materialClip, ink };
    }
    protected override int VisualChildrenCount => layers.Count;
    protected override Visual GetVisualChild(int index) => layers[index];
    public void EnableHardware()
    {
        if (!FluidEffect.Available) return;
        effect = new FluidEffect(Palette, MotionStyle); material.Effect = effect;
    }
    public double Morph { get; set; }
    public double Drain { get; set; }
    public double Entrance { get; set; } = 1;
    public double Time { get; set; }
    public int Seconds { get; set; } = 29;
    public int Duration { get; set; } = 20;
    public int Palette { get; set; }
    public double? Progress { get; set; }
    public bool Secondary { get; set; }
    public bool ReducedMotion { get; set; }
    public bool FlowingBackground { get; set; } = true;
    public bool BreathingRing { get; set; } = true;
    public bool DesktopPreview { get; set; }
    public Rect? SceneBounds { get; set; }
    public Rect? CapsuleBounds { get; set; }
    public BitmapSource? FluidFrame { get; set; }

    protected override void OnRender(DrawingContext dc)
    {
        using var foreground = ink.RenderOpen();
        DrawScene(foreground);
    }
    void DrawScene(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 24 || h < 24) return;
        if (DesktopPreview) Desktop(dc, w, h);
        double p = Math.Clamp(Morph, 0, 1);
        double spread = Math.Clamp((p - .18) / .82, 0, 1);
        double eased = 1 - Math.Pow(1 - spread, 3.5);
        double startW = Math.Min(300, w - 24), startH = 64;
        var origin = CapsuleBounds ?? new Rect((w - startW) / 2, 20, startW, startH);
        origin.Y -= (1 - Entrance) * 130;
        var scene = SceneBounds ?? new Rect(0, 0, w, h);
        // Each screen unfolds from its own center, independent of the virtual-desktop seam.
        double travel = 1 - Math.Pow(1 - Math.Clamp(p / .18, 0, 1), 3);
        origin.Y = Secondary && p > 0 ? (h - origin.Height) / 2 : Lerp(origin.Y, (h - origin.Height) / 2, travel);
        var target = new Rect(-2, -2, w + 4, h + 4);
        var r = new Rect(Lerp(origin.X, target.X, eased), Lerp(origin.Y, target.Y, eased),
            Lerp(origin.Width, target.Width, eased), Lerp(origin.Height, target.Height, eased));
        double radius = Lerp(origin.Height / 2, 0, eased);
        Geometry shape = new RectangleGeometry(r, radius, radius);
        if (spread > .015 && spread < .98 && !ReducedMotion) shape = Liquid(r, radius, Math.Sin(spread * Math.PI) * Math.Min(w, h) * .035, Time);
        if (Drain > 0) shape = new CombinedGeometry(GeometryCombineMode.Intersect, shape, Pour(w, h, Drain, Time));
        dc.PushOpacity(Secondary ? Math.Min(1, p * 8) : Entrance);
        dc.PushClip(shape);
        if (effect != null)
        {
            materialClip.Clip = shape;
            materialClip.Opacity = Secondary ? Math.Min(1, p * 8) : Entrance;
            effect.Time = ReducedMotion || !FlowingBackground ? 0 : Time;
            effect.Aspect = Math.Clamp(scene.Width / scene.Height, .7, 3.8);
            if (lastScene != scene)
            {
                using var background = material.RenderOpen();
                background.DrawRectangle(Brushes.White, null, scene);
                lastScene = scene;
            }
        }
        else { materialClip.Opacity = 0; DrawGradient(dc, scene); }
        if (p < .8)
        {
            dc.PushOpacity(1 - Math.Min(1, p * 2.7));
            dc.DrawRectangle(Art.Brush("#F21B2331"), null, r);
            DrawPill(dc, origin);
            dc.Pop();
        }
        if (p > .25)
        {
            dc.PushOpacity(Math.Clamp((p - .25) / .65, 0, 1));
            DrawRest(dc, w, h);
            dc.Pop();
        }
        dc.Pop(); dc.Pop();
        if (p == 0 && !Secondary)
        {
            var edge = new LinearGradientBrush(Art.Brush("#8D6FFFE0").Color, Art.Brush("#968C76FF").Color, 12);
            dc.DrawRoundedRectangle(null, new Pen(Art.Brush("#0D75E9DD"), 7), r, 32, 32);
            dc.DrawRoundedRectangle(null, new Pen(edge, 1), r, 32, 32);
        }
    }
    void DrawPill(DrawingContext dc, Rect r)
    {
        var c = new Point(r.X + 36, r.Y + 32);
        dc.DrawEllipse(Art.Brush("#153A454C"), null, c, 20, 20);
        Art.Ring(dc, c, 19, Seconds / 30d, 1.6, Art.Brush("#8DF9E2"));
        Art.Text(dc, Seconds.ToString("00"), 15, Brushes.White, c.X, c.Y - 11, true, true);
        Art.Text(dc, "Rest time", 15, Art.Brush("#F1F8FF"), r.X + 70, r.Y + 12, true);
        Art.Text(dc, "A moment for your eyes", 10.5, Art.Brush("#8C9AAF"), r.X + 70, r.Y + 35);
        Art.Logo(dc, r.Right - 42, r.Y + 20, 24);
    }
    void DrawGradient(DrawingContext dc, Rect r)
    {
        var frame = FluidFrame ?? FluidTexture.Create(Palette, ReducedMotion || !FlowingBackground ? 0 : Time, 960, Math.Max(200, (int)(960 * r.Height / r.Width)), MotionStyle);
        dc.DrawImage(frame, r);
    }
    void DrawRest(DrawingContext dc, double w, double h)
    {
        double scale = Math.Min(1, Math.Min(w / 1000, h / 700));
        double cx = w / 2, cy = h / 2;
        // A local transparent veil keeps the timer readable without desaturating the artwork.
        var veil = new RadialGradientBrush(Color.FromArgb(105, 2, 5, 20), Colors.Transparent);
        dc.DrawEllipse(veil, null, new Point(cx, cy + 45 * scale), 330 * scale, 370 * scale);
        Art.Logo(dc, 35, 30, 32);
        Art.Text(dc, "eyekeeper", 16, Art.Brush("#DDFFFFFF"), 77, 34, true);
        Art.Text(dc, "A MOMENT OF CALM", 10, Art.Brush("#B8FFFFFF"), w - 164, 39);
        dc.PushTransform(new TranslateTransform(cx, cy));
        dc.PushTransform(new ScaleTransform(scale, scale));
        double wave = ReducedMotion || !BreathingRing ? 0 : .5 + .5 * Math.Sin(Time * Math.PI * 2 / 2.8);
        double breathe = 1 + .012 * wave;
        double progress = Progress ?? Seconds / (double)Math.Max(1, Duration);
        dc.DrawEllipse(null, new Pen(Art.Brush("#13FFFFFF"), 1), new Point(0, -17), 174 * breathe, 174 * breathe);
        dc.PushOpacity(.10 + .05 * wave);
        Art.Ring(dc, new Point(0, -17), 153 * breathe, progress, 11 + 2 * wave, Brushes.White);
        dc.Pop();
        Art.Ring(dc, new Point(0, -17), 153 * breathe, progress, 4.2 + .8 * wave, Art.Brush("#D9FFFFFF"));
        Art.Text(dc, "B R E A T H E   &   R E S E T", 11, Art.Brush("#CCFFFFFF"), 0, -111, center: true);
        Art.Text(dc, Seconds.ToString("00"), 118, Brushes.White, 0, -92, center: true);
        Art.Text(dc, "SECONDS OF STILLNESS", 10, Art.Brush("#BFFFFFFF"), 0, 52, center: true);
        Art.Text(dc, "Let your eyes wander.", 30, Brushes.White, 0, 184, center: true);
        Art.Text(dc, "Soften your shoulders. Breathe. Focus on something far away.",
            14, Art.Brush("#E6FFFFFF"), 0, 237, center: true);
        dc.Pop(); dc.Pop();
        Art.Text(dc, "20  /  20  /  20", 11, Art.Brush("#ACFFFFFF"), 40, h - 51);
        Art.Text(dc, "ESC  ·  Skip     |     S  ·  Snooze 5 min", 12, Art.Brush("#D0FFFFFF"), w / 2, h - 51, center: true);
        Art.Text(dc, "YOUR EYES, AT EASE", 10, Art.Brush("#9CFFFFFF"), w - 160, h - 50);
    }
    static Geometry Liquid(Rect r, double radius, double amplitude, double time)
    {
        // Cubic edges stretch the capsule into a soft, slightly asymmetric sheet.
        double rr = Math.Min(radius, Math.Min(r.Width, r.Height) / 2);
        double a = amplitude * Math.Sin(time * 2 + .7), b = amplitude * Math.Cos(time * 1.7);
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(r.Left + rr, r.Top), true, true);
            c.BezierTo(new Point(r.Left + r.Width * .35, r.Top - a), new Point(r.Left + r.Width * .7, r.Top + b), new Point(r.Right - rr, r.Top), true, false);
            c.QuadraticBezierTo(r.TopRight, new Point(r.Right, r.Top + rr), true, false);
            c.BezierTo(new Point(r.Right + a, r.Top + r.Height * .4), new Point(r.Right - b, r.Top + r.Height * .7), new Point(r.Right, r.Bottom - rr), true, false);
            c.QuadraticBezierTo(r.BottomRight, new Point(r.Right - rr, r.Bottom), true, false);
            c.BezierTo(new Point(r.Left + r.Width * .7, r.Bottom + a), new Point(r.Left + r.Width * .3, r.Bottom - b), new Point(r.Left + rr, r.Bottom), true, false);
            c.QuadraticBezierTo(r.BottomLeft, new Point(r.Left, r.Bottom - rr), true, false);
            c.BezierTo(new Point(r.Left - b, r.Top + r.Height * .7), new Point(r.Left + a, r.Top + r.Height * .3), new Point(r.Left, r.Top + rr), true, false);
            c.QuadraticBezierTo(r.TopLeft, new Point(r.Left + rr, r.Top), true, false);
        }
        return g;
    }
    static double Lerp(double a, double b, double p) => a + (b - a) * p;
    static Geometry Pour(double w, double h, double progress, double time)
    {
        double p = Math.Clamp(progress, 0, 1);
        double fall = p * p * (1.3 - .3 * p);
        double y = Lerp(-90, h + 160, fall);
        double a = Math.Sin(p * Math.PI) * Math.Min(90, h * .12);
        double sway = Math.Sin(time * 1.8) * a * .3;
        var body = new StreamGeometry();
        using (var c = body.Open())
        {
            c.BeginFigure(new Point(-10, y), true, true);
            c.BezierTo(new Point(w * .08, y - a), new Point(w * .18, y + a * 1.6), new Point(w * .31, y + sway), true, false);
            c.BezierTo(new Point(w * .43, y - a * 1.5), new Point(w * .49, y + a * 1.8), new Point(w * .62, y + a * .4), true, false);
            c.BezierTo(new Point(w * .77, y - a * 1.8), new Point(w * .88, y + a * 1.2), new Point(w + 10, y), true, false);
            c.LineTo(new Point(w + 10, h + 200), true, false);
            c.LineTo(new Point(-10, h + 200), true, false);
        }
        var result = new GeometryGroup { FillRule = FillRule.Nonzero };
        result.Children.Add(body);
        double drop = Math.Sin(p * Math.PI);
        result.Children.Add(new EllipseGeometry(new Point(w * .22, y - 72 * drop), 13 * drop, 32 * drop));
        result.Children.Add(new EllipseGeometry(new Point(w * .73, y - 110 * drop), 9 * drop, 23 * drop));
        return result;
    }
    static void Desktop(DrawingContext dc, double w, double h)
    {
        dc.DrawRectangle(new LinearGradientBrush(Art.Brush("#101A2B").Color, Art.Brush("#243D50").Color, 60), null, new Rect(0, 0, w, h));
        dc.DrawEllipse(new RadialGradientBrush(Art.Brush("#344C62").Color, Art.Brush("#00344C62").Color), null, new Point(w * .65, h * .55), w * .6, h * .8);
        Art.Text(dc, "Room to focus.", 52, Art.Brush("#38FFFFFF"), w / 2, h / 2 - 20, center: true);
        Art.Text(dc, "E Y E K E E P E R   /   D E S K T O P   P R E V I E W", 11, Art.Brush("#60FFFFFF"), w / 2, h / 2 + 66, center: true);
        dc.DrawRectangle(Art.Brush("#80202B3A"), null, new Rect(0, h - 48, w, 48));
        Art.Logo(dc, w - 170, h - 39, 28);
        Art.Text(dc, "09:41", 12, Art.Brush("#C5D2E2"), w - 66, h - 34);
    }
}
