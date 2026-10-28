using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace EyeKeeper;

public sealed class FluidEffect : ShaderEffect
{
    public bool IsLoaded => PixelShader != null;
    static readonly PixelShader[] Shaders = [Load(0), Load(1), Load(2), Load(3), Load(4), Load(5), Load(6), Load(7)];
    public static bool Available => (RenderCapability.Tier >> 16) >= 2 && RenderCapability.IsPixelShaderVersionSupported(3, 0)
        && !System.Windows.Forms.SystemInformation.TerminalServerSession;
    static PixelShader Load(int mode)
    {
        var shader = new PixelShader();
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"EyeKeeper.Assets.Fluid{mode}.ps")
            ?? throw new InvalidOperationException("Compiled fluid shader is missing.");
        shader.SetStreamSource(stream); shader.Freeze(); return shader;
    }
    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(nameof(Time), typeof(double), typeof(FluidEffect), new UIPropertyMetadata(0d, PixelShaderConstantCallback(0)));
    public static readonly DependencyProperty AspectProperty = DependencyProperty.Register(nameof(Aspect), typeof(double), typeof(FluidEffect), new UIPropertyMetadata(1.6d, PixelShaderConstantCallback(1)));
    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(double), typeof(FluidEffect), new UIPropertyMetadata(0d, PixelShaderConstantCallback(2)));
    public static readonly DependencyProperty AProperty = ColorProperty(nameof(A), 3);
    public static readonly DependencyProperty BProperty = ColorProperty(nameof(B), 4);
    public static readonly DependencyProperty CProperty = ColorProperty(nameof(C), 5);
    public static readonly DependencyProperty DProperty = ColorProperty(nameof(D), 6);
    static DependencyProperty ColorProperty(string name, int register) => DependencyProperty.Register(name, typeof(Point3D), typeof(FluidEffect), new UIPropertyMetadata(new Point3D(), PixelShaderConstantCallback(register)));
    public double Time { get => (double)GetValue(TimeProperty); set => SetValue(TimeProperty, value); }
    public double Aspect { get => (double)GetValue(AspectProperty); set => SetValue(AspectProperty, value); }
    public double Mode { get => (double)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public Point3D A { get => (Point3D)GetValue(AProperty); set => SetValue(AProperty, value); }
    public Point3D B { get => (Point3D)GetValue(BProperty); set => SetValue(BProperty, value); }
    public Point3D C { get => (Point3D)GetValue(CProperty); set => SetValue(CProperty, value); }
    public Point3D D { get => (Point3D)GetValue(DProperty); set => SetValue(DProperty, value); }
    public FluidEffect(int palette, int mode)
    {
        PixelShader = Shaders[Math.Clamp(mode, 0, 7)]; Mode = mode;
        var colors = Palettes.Colors[palette];
        Point3D Vector(string hex) { var c = Art.Brush(hex).Color; return new Point3D(c.R / 255d, c.G / 255d, c.B / 255d); }
        A = Vector(colors[0]); B = Vector(colors[1]); C = Vector(colors[2]); D = Vector(colors[3]);
        foreach (var property in new[] { TimeProperty, AspectProperty, ModeProperty, AProperty, BProperty, CProperty, DProperty }) UpdateShaderValue(property);
    }
}
