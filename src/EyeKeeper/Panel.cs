using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace EyeKeeper;

public sealed class ControlPanel : Border
{
    readonly TextBlock countdown;
    readonly TextBlock status;
    readonly Button pause;
    readonly Preferences prefs;
    readonly BreakEngine engine;
    public Action? SaveRequested;
    public Action? QuitRequested;
    public Action? CloseRequested;
    public ControlPanel(Preferences preferences, BreakEngine timer)
    {
        prefs = preferences; engine = timer;
        Width = 390; Padding = new Thickness(25); CornerRadius = new CornerRadius(22);
        Background = Art.Brush("#121B29"); BorderBrush = Art.Brush("#344151"); BorderThickness = new Thickness(1);
        Resources = (ResourceDictionary)XamlReader.Parse("""
          <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <Style TargetType="Button">
              <Setter Property="Foreground" Value="#E5F0F6"/><Setter Property="Background" Value="#233246"/>
              <Setter Property="Padding" Value="14,10"/><Setter Property="Cursor" Value="Hand"/><Setter Property="FontSize" Value="12"/>
              <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
                <Border x:Name="bg" Background="{TemplateBinding Background}" CornerRadius="10" Padding="{TemplateBinding Padding}">
                  <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bg" Property="Opacity" Value="0.75"/></Trigger>
                  <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="bg" Property="BorderBrush" Value="#B9F2E2"/><Setter TargetName="bg" Property="BorderThickness" Value="1"/></Trigger>
                </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
            </Style>
            <Style TargetType="CheckBox">
              <Setter Property="Foreground" Value="#DFE9F4"/><Setter Property="FontSize" Value="12"/>
              <Setter Property="Margin" Value="0,8,0,8"/><Setter Property="Cursor" Value="Hand"/>
              <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox">
                <DockPanel><Border x:Name="track" DockPanel.Dock="Right" Width="32" Height="18" CornerRadius="9" Background="#3D4A60">
                  <Ellipse x:Name="dot" Fill="#DBE6EF" Width="12" Height="12" HorizontalAlignment="Left" Margin="3,0"/>
                </Border><ContentPresenter VerticalAlignment="Center"/></DockPanel>
                <ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="track" Property="Background" Value="#B9F2E2"/>
                <Setter TargetName="dot" Property="Fill" Value="#16252F"/><Setter TargetName="dot" Property="HorizontalAlignment" Value="Right"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="track" Property="BorderBrush" Value="White"/><Setter TargetName="track" Property="BorderThickness" Value="1"/></Trigger>
                </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
            </Style>
            <Style TargetType="ComboBox">
              <Setter Property="FontSize" Value="12"/><Setter Property="Padding" Value="8,6"/>
              <Setter Property="MinWidth" Value="122"/><Setter Property="Foreground" Value="#E3EDF6"/>
              <Setter Property="Background" Value="#233246"/>
              <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox">
                <Grid>
                  <ToggleButton Focusable="False" IsChecked="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}">
                    <ToggleButton.Template><ControlTemplate TargetType="ToggleButton">
                      <Border Background="#233246" CornerRadius="8" BorderBrush="#34465C" BorderThickness="1">
                        <Path Data="M0,0 L4,4 L8,0" Stroke="#A1B8C9" StrokeThickness="1.2" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,12,0"/>
                      </Border>
                    </ControlTemplate></ToggleButton.Template>
                  </ToggleButton>
                  <ContentPresenter Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}" IsHitTestVisible="False" Margin="11,7,28,7" VerticalAlignment="Center"/>
                  <Popup x:Name="PART_Popup" IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom" AllowsTransparency="True" Focusable="False" PopupAnimation="Fade">
                    <Border Background="#1D2A3D" BorderBrush="#43516A" BorderThickness="1" CornerRadius="8" Padding="4" MinWidth="122" MaxHeight="240">
                      <ScrollViewer VerticalScrollBarVisibility="Auto"><ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/></ScrollViewer>
                    </Border>
                  </Popup>
                </Grid>
                <ControlTemplate.Triggers><Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter Property="Foreground" Value="#B9F2E2"/></Trigger></ControlTemplate.Triggers>
              </ControlTemplate></Setter.Value></Setter>
            </Style>
            <Style TargetType="ComboBoxItem">
              <Setter Property="Foreground" Value="#DFEBF4"/><Setter Property="Padding" Value="8,7"/>
              <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBoxItem">
                <Border x:Name="item" CornerRadius="5" Background="Transparent" Padding="{TemplateBinding Padding}"><ContentPresenter/></Border>
                <ControlTemplate.Triggers>
                  <Trigger Property="IsHighlighted" Value="True"><Setter TargetName="item" Property="Background" Value="#34485D"/></Trigger>
                  <Trigger Property="IsSelected" Value="True"><Setter Property="Foreground" Value="#B9F2E2"/></Trigger>
                </ControlTemplate.Triggers>
              </ControlTemplate></Setter.Value></Setter>
            </Style>
          </ResourceDictionary>
          """);
        var root = new StackPanel(); Child = root;
        var header = new DockPanel();
        var badge = new TextBlock { Text = "●  AT EASE", FontSize = 9, Foreground = Art.Brush("#B9F2E2"), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(badge, Dock.Right); header.Children.Add(badge);
        var brand = new StackPanel { Orientation = Orientation.Horizontal };
        brand.Children.Add(new LogoElement { Width = 38, Height = 38, Margin = new Thickness(-3, 0, 8, 0) });
        brand.Children.Add(Label("eyekeeper", 22, "#F0F6FA", true)); header.Children.Add(brand); root.Children.Add(header);
        root.Children.Add(Label("A quiet moment for your eyes.", 11, "#8495AC", margin: new Thickness(1, 3, 0, 20)));
        var timerBox = new Border { Background = Art.Brush("#1A2739"), CornerRadius = new CornerRadius(15), Padding = new Thickness(18, 15, 18, 16) };
        var timeStack = new StackPanel(); timerBox.Child = timeStack;
        status = Label("NEXT MOMENT OF CALM", 9, "#A1B2C9"); timeStack.Children.Add(status);
        countdown = Label("19:42", 45, "#EDF7F8"); countdown.FontWeight = FontWeights.Light; timeStack.Children.Add(countdown);
        timeStack.Children.Add(Label("Small pauses. A gentler day.", 12, "#A8BDCB"));
        root.Children.Add(timerBox);
        var actions = new Grid { Margin = new Thickness(0, 12, 0, 18) };
        actions.ColumnDefinitions.Add(new ColumnDefinition()); actions.ColumnDefinitions.Add(new ColumnDefinition());
        var rest = new Button { Content = "Take a break", Background = Art.Brush("#B9F2E2"), Foreground = Art.Brush("#172B30"), Margin = new Thickness(0, 0, 5, 0) };
        rest.Click += (_, _) => engine.RestNow(); actions.Children.Add(rest);
        pause = new Button { Content = "Pause 30 min", Margin = new Thickness(5, 0, 0, 0) };
        pause.Click += (_, _) => { if (engine.Phase == Phase.Paused) engine.Resume(); else engine.Pause(30); Refresh(); };
        Grid.SetColumn(pause, 1); actions.Children.Add(pause); root.Children.Add(actions);
        root.Children.Add(Label("YOUR RHYTHM", 9, "#8295AC", margin: new Thickness(0, 0, 0, 10)));
        root.Children.Add(Choice("Break every", [1, 5, 10, 15, 20, 25, 30, 45, 60, 90, 120, 180], prefs.WorkMinutes, " min", n => prefs.WorkMinutes = n));
        root.Children.Add(Choice("Rest for", [5, 10, 15, 20, 30, 45, 60, 90, 120, 180, 300], prefs.RestSeconds, " sec", n => prefs.RestSeconds = n));
        var palette = new ComboBox { ItemsSource = Palettes.Names, SelectedItem = prefs.Palette };
        palette.SelectionChanged += (_, _) => prefs.Palette = (string)palette.SelectedItem;
        root.Children.Add(Row("Color atmosphere", palette));
        var motion = new ComboBox { ItemsSource = FluidStyles.Names, SelectedItem = prefs.MotionStyle };
        motion.SelectionChanged += (_, _) => prefs.MotionStyle = (string)motion.SelectedItem;
        root.Children.Add(Row("Liquid motion", motion));
        root.Children.Add(new Border { Height = 1, Background = Art.Brush("#2A3749"), Margin = new Thickness(0, 13, 0, 7) });
        root.Children.Add(Toggle("Launch with Windows", prefs.StartWithWindows, v => prefs.StartWithWindows = v));
        root.Children.Add(Toggle("Reduced motion", prefs.ReducedMotion, v => prefs.ReducedMotion = v));
        var details = new StackPanel { Margin = new Thickness(0, 9, 0, 3) };
        details.Children.Add(Toggle("Flowing background", prefs.FlowingBackground, v => prefs.FlowingBackground = v));
        details.Children.Add(Toggle("Breathing ring", prefs.BreathingRing, v => prefs.BreathingRing = v));
        details.Children.Add(Toggle("Liquid transitions", prefs.LiquidTransitions, v => prefs.LiquidTransitions = v));
        root.Children.Add(new Expander { Header = "Animation details", Content = details, Foreground = Art.Brush("#ACC0D1"), FontSize = 11, Margin = new Thickness(0, 8, 0, 0) });
        var audio = new StackPanel { Margin = new Thickness(0, 9, 0, 3) };
        audio.Children.Add(Toggle("Sound effects", prefs.SoundEnabled, v => prefs.SoundEnabled = v));
        audio.Children.Add(Toggle("Rest countdown ticks", prefs.TickSounds, v => prefs.TickSounds = v));
        audio.Children.Add(Label("Sound volume", 11, "#8295AC", margin: new Thickness(0, 6, 0, 6)));
        var volume = new Slider { Minimum = 0, Maximum = 100, Value = prefs.SoundVolume * 100, Foreground = Art.Brush("#B9F2E2"), Margin = new Thickness(0, 0, 0, 8) };
        volume.ValueChanged += (_, e) => prefs.SoundVolume = e.NewValue / 100;
        audio.Children.Add(volume);
        root.Children.Add(new Expander { Header = "Sound", Content = audio, Foreground = Art.Brush("#ACC0D1"), FontSize = 11, Margin = new Thickness(0, 10, 0, 0) });
        var appDetails = new StackPanel { Margin = new Thickness(0, 10, 0, 3) };
        appDetails.Children.Add(Label("Closing this menu keeps reminders running.", 10, "#8295AC"));
        var quit = new Button { Content = "Quit EyeKeeper completely…", Background = Art.Brush("#352534"), Foreground = Art.Brush("#EDB5C4"), Margin = new Thickness(0, 10, 0, 0) };
        quit.Click += (_, _) => QuitRequested?.Invoke(); appDetails.Children.Add(quit);
        root.Children.Add(new Expander { Header = "Application", Content = appDetails, Foreground = Art.Brush("#ACC0D1"), FontSize = 11, Margin = new Thickness(0, 10, 0, 0) });
        root.Children.Add(Label("A gentle heads-up, 30 seconds before each break.", 10, "#8295AC", margin: new Thickness(0, 10, 0, 17)));
        var footer = new DockPanel();
        var close = new Button { Content = "Close menu", Background = Brushes.Transparent, Foreground = Art.Brush("#9AAAC0"), Padding = new Thickness(8, 10, 8, 10) };
        close.Click += (_, _) => CloseRequested?.Invoke(); DockPanel.SetDock(close, Dock.Right); footer.Children.Add(close);
        var save = new Button { Content = "Save preferences", HorizontalAlignment = HorizontalAlignment.Left };
        save.Click += (_, _) => SaveRequested?.Invoke(); footer.Children.Add(save); root.Children.Add(footer);
        Refresh();
    }
    public void Refresh()
    {
        countdown.Text = engine.Phase == Phase.Paused ? "Paused" : $"{engine.Remaining / 60:00}:{engine.Remaining % 60:00}";
        status.Text = engine.Phase switch { Phase.Resting => "A MOMENT FOR YOU", Phase.Warning => "REST IS ON ITS WAY", Phase.Paused => "YOUR RHYTHM, ON HOLD", _ => "NEXT MOMENT OF CALM" };
        pause.Content = engine.Phase == Phase.Paused ? "Resume" : "Pause 30 min";
    }
    static TextBlock Label(string text, double size, string color, bool bold = false, Thickness? margin = null) => new()
    {
        Text = text, FontFamily = new FontFamily("Segoe UI"), FontSize = size, Foreground = Art.Brush(color),
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Margin = margin ?? new Thickness(0), VerticalAlignment = VerticalAlignment.Center
    };
    static DockPanel Row(string text, FrameworkElement control)
    {
        var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4), MinHeight = 34 };
        DockPanel.SetDock(control, Dock.Right); row.Children.Add(control); row.Children.Add(Label(text, 12, "#D7E2EF")); return row;
    }
    static FrameworkElement Choice(string text, int[] values, int value, string suffix, Action<int> change)
    {
        var combo = new ComboBox();
        if (Array.IndexOf(values, value) < 0) { var list = new System.Collections.Generic.List<int>(values) { value }; list.Sort(); values = list.ToArray(); }
        foreach (int n in values) combo.Items.Add(new ComboBoxItem { Content = n + suffix, Tag = n });
        foreach (ComboBoxItem item in combo.Items) if ((int)item.Tag == value) combo.SelectedItem = item;
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is ComboBoxItem item) change((int)item.Tag); };
        return Row(text, combo);
    }
    static CheckBox Toggle(string text, bool value, Action<bool> change)
    {
        var check = new CheckBox { Content = text, IsChecked = value };
        check.Checked += (_, _) => change(true); check.Unchecked += (_, _) => change(false); return check;
    }
}

public sealed class LogoElement : FrameworkElement
{
    protected override void OnRender(DrawingContext dc) => Art.Logo(dc, 0, 0, Math.Min(ActualWidth, ActualHeight), true);
}
