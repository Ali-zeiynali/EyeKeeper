using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EyeKeeper;

public static class Preview
{
    public static BitmapSource Render(FrameworkElement element, int width, int height)
    {
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element); return bitmap;
    }
    static byte[] Png(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
    }
    static void Save(BitmapSource bitmap, string path) => File.WriteAllBytes(path, Png(bitmap));
    public static void Generate(string folder)
    {
        Directory.CreateDirectory(folder);
        // Regression: native windows briefly arrange at 1x1 before monitor placement.
        Render(new RestVisual(), 1, 1);
        var warning = Render(new RestVisual { DesktopPreview = true, Seconds = 29 }, 1440, 900);
        Save(warning, Path.Combine(folder, "01-warning.png"));
        var paletteImages = new List<BitmapSource>();
        for (int i = 0; i < Palettes.Colors.Length; i++)
        {
            var shot = Render(new RestVisual { Morph = 1, Seconds = 17, Duration = 20, Progress = .825, Palette = i, Time = 3 }, 1440, 900);
            paletteImages.Add(shot);
            Save(shot, Path.Combine(folder, $"rest-{Palettes.PaletteNames[i].ToLowerInvariant()}.png"));
        }
        var gallery = new DrawingVisual();
        using (var dc = gallery.RenderOpen())
        {
            dc.DrawRectangle(Art.Brush("#0A101A"), null, new Rect(0, 0, 1600, 2230));
            Art.Text(dc, "18 atmospheres. One quiet moment.", 28, Brushes.White, 40, 25, true);
            Art.Text(dc, "VIVID / LIGHT / DARK     ·     Fixed colors or a fresh atmosphere with every break", 12, Art.Brush("#91A8BC"), 40, 70);
            for (int i = 0; i < paletteImages.Count; i++)
            {
                double x = 40 + (i % 3) * 520, y = 115 + (i / 3) * 350;
                dc.DrawImage(paletteImages[i], new Rect(x, y, 480, 300));
                Art.Text(dc, Palettes.PaletteNames[i], 13, Brushes.White, x, y + 311, true);
            }
        }
        var galleryBitmap = new RenderTargetBitmap(1600, 2230, 96, 96, PixelFormats.Pbgra32); galleryBitmap.Render(gallery);
        Save(galleryBitmap, Path.Combine(folder, "05-palette-gallery.png"));
        var modes = new DrawingVisual();
        using (var dc = modes.RenderOpen())
        {
            dc.DrawRectangle(Art.Brush("#0A101A"), null, new Rect(0, 0, 1600, 2150));
            Art.Text(dc, "Eight ways to flow.", 28, Brushes.White, 40, 26, true);
            Art.Text(dc, "Every motion style works with all 18 atmospheres. Choose either, or let them change with every break.", 12, Art.Brush("#91A8BC"), 40, 74);
            int[] samples = [0, 2, 12, 5, 6, 3, 1, 17];
            for (int i = 0; i < FluidStyles.Styles.Length; i++)
            {
                var shot = Render(new RestVisual { Morph = 1, Palette = samples[i], MotionStyle = i, Time = 3, Seconds = 17, Progress = .85 }, 1440, 900);
                Save(shot, Path.Combine(folder, $"motion-{FluidStyles.Styles[i].ToLowerInvariant()}.png"));
                double x = 40 + (i % 2) * 780, y = 115 + (i / 2) * 500;
                dc.DrawImage(shot, new Rect(x, y, 720, 450));
                Art.Text(dc, FluidStyles.Styles[i], 14, Brushes.White, x, y + 462, true);
            }
        }
        var modesBitmap = new RenderTargetBitmap(1600, 2150, 96, 96, PixelFormats.Pbgra32); modesBitmap.Render(modes);
        Save(modesBitmap, Path.Combine(folder, "07-motion-gallery.png"));
        var capsule = new DrawingVisual();
        using (var dc = capsule.RenderOpen())
        {
            dc.DrawRectangle(Art.Brush("#0A101B"), null, new Rect(0, 0, 800, 160));
            dc.DrawImage(Render(new RestVisual { Seconds = 29 }, 400, 104), new Rect(200, 28, 400, 104));
        }
        var capsuleBitmap = new RenderTargetBitmap(800, 160, 96, 96, PixelFormats.Pbgra32); capsuleBitmap.Render(capsule);
        Save(capsuleBitmap, Path.Combine(folder, "08-warning-capsule.png"));
        Save(Render(new RestVisual { DesktopPreview = true, Morph = .16, Seconds = 20, Time = 1.3 }, 1440, 900), Path.Combine(folder, "02-liquid-transition.png"));
        var prefs = new Preferences();
        var engine = new BreakEngine(prefs, () => 0);
        var panel = new ControlPanel(prefs, engine);
        panel.Measure(new Size(390, double.PositiveInfinity));
        int panelH = (int)Math.Ceiling(panel.DesiredSize.Height);
        var panelImage = Render(panel, 390, panelH);
        Save(panelImage, Path.Combine(folder, "03-tray-panel.png"));
        var board = new DrawingVisual();
        using (var dc = board.RenderOpen())
        {
            dc.DrawRectangle(Art.Brush("#0A101A"), null, new Rect(0, 0, 1600, 1060));
            Art.Logo(dc, 48, 28, 58, true);
            Art.Text(dc, "eyekeeper", 30, Brushes.White, 120, 28, true);
            Art.Text(dc, "A quiet moment for your eyes.", 13, Art.Brush("#8394AE"), 121, 71);
            Art.Text(dc, "NATIVE WINDOWS  /  DESIGN PREVIEW", 11, Art.Brush("#A5DACB"), 1213, 53);
            dc.DrawImage(warning, new Rect(48, 130, 1024, 640));
            dc.DrawImage(panelImage, new Rect(1135, 130, 390, panelH));
            Art.Text(dc, "01   A gentle heads-up", 14, Brushes.White, 48, 790, true);
            Art.Text(dc, "A 30-second countdown, without interrupting your flow.", 12, Art.Brush("#8394AE"), 48, 817);
            int k = 0;
            foreach (var (morph, label) in new (double, string)[] { (.06, "02  Unfold"), (.22, "03  Flow"), (1, "04  Rest") })
            {
                var img = Render(new RestVisual { Morph = morph, DesktopPreview = true, Seconds = 20, Time = 2 }, 960, 600);
                dc.DrawImage(img, new Rect(48 + k * 348, 866, 324, 166));
                Art.Text(dc, label, 11, Brushes.White, 60 + k * 348, 1008); k++;
            }
        }
        var overview = new RenderTargetBitmap(1600, 1060, 96, 96, PixelFormats.Pbgra32); overview.Render(board);
        Save(overview, Path.Combine(folder, "00-overview.png"));
        var multi = new DrawingVisual();
        using (var dc = multi.RenderOpen())
        {
            dc.DrawRectangle(Art.Brush("#090F19"), null, new Rect(0, 0, 1960, 690));
            Art.Text(dc, "ONE MOMENT. EVERY DISPLAY.", 15, Art.Brush("#B9F2E2"), 40, 22, true);
            dc.DrawImage(Render(new RestVisual { Morph = 1, Seconds = 17, Progress = .85, Time = 4, SceneBounds = new Rect(0, 0, 1920, 600) }, 960, 600), new Rect(16, 65, 960, 600));
            dc.DrawImage(Render(new RestVisual { Morph = 1, Seconds = 17, Progress = .85, Time = 4, Secondary = true, SceneBounds = new Rect(-960, 0, 1920, 600) }, 960, 600), new Rect(984, 65, 960, 600));
        }
        var two = new RenderTargetBitmap(1960, 690, 96, 96, PixelFormats.Pbgra32); two.Render(multi);
        Save(two, Path.Combine(folder, "04-two-displays.png"));
        Save(Render(new LogoElement(), 512, 512), Path.Combine(folder, "eyekeeper-icon.png"));
        GenerateIcon(Path.Combine(folder, "eyekeeper.ico"));
        string frames = Path.Combine(folder, "frames"); Directory.CreateDirectory(frames);
        for (int frame = 0; frame < 128; frame++)
        {
            double t = frame / 16d;
            Save(Render(new RestVisual { DesktopPreview = true, Morph = Math.Clamp((t - .75) / 1.5, 0, 1),
                Drain = Math.Clamp((t - 6.4) / 1.05, 0, 1),
                Seconds = t < .75 ? 1 : (int)Math.Ceiling(20 - Math.Max(0, t - .75)), Progress = (20 - Math.Max(0, t - .75)) / 20, Time = t }, 960, 600), Path.Combine(frames, $"{frame:000}.png"));
        }
        Save(Render(new RestVisual { DesktopPreview = true, Morph = 1, Drain = .58, Time = 6.8, Seconds = 0 }, 1440, 900), Path.Combine(folder, "09-liquid-exit.png"));
        File.WriteAllText(Path.Combine(folder, "render-report.txt"), $"Rendered actual WPF controls offscreen using the CPU reference of the GPU shader.\nNo Window.Show, tray icon, settings writes or startup registration in render mode.\nPanel size: 390 x {panelH}.\n{Palettes.Colors.Length} procedural liquid palettes, 8 styles (144 combinations), 128 animation frames, dual-display composition.\nTiny 1x1 native-window regression render passed.\nDual-display image is a simulated layout, not a physical multi-monitor integration test.\n");
    }
    static void GenerateIcon(string path)
    {
        int[] sizes = [16, 20, 24, 32, 48, 64, 128, 256];
        var images = new List<byte[]>(); foreach (int size in sizes) images.Add(Png(Render(new LogoElement(), size, size)));
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
        int offset = 6 + sizes.Length * 16;
        for (int i = 0; i < sizes.Length; i++)
        {
            writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
            writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
            writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length;
        }
        foreach (var image in images) writer.Write(image);
    }
}
