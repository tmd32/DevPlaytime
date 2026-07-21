using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Control = System.Windows.Controls.Control;
using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;

namespace DevPlaytimeDesktop;

public sealed class AppEditorWindow : Window
{
    private readonly TrackerApp? _editing;
    private readonly TextBox _name = new();
    private readonly TextBox _processes = new();
    private readonly TextBox _type = new();
    private readonly TextBox _icon = new();
    private readonly TextBox _description = new();
    private readonly TextBox _color = new();
    private readonly TextBox _projectPath = new();
    private readonly Border _colorPreview = new();
    private TextBlock? _pickerHexText;
    private Canvas? _wheelCanvas;
    private Canvas? _valueCanvas;
    private Image? _valueBarImage;
    private Border? _wheelMarker;
    private Border? _valueMarker;
    private Popup? _colorPickerPopup;
    private Popup? _presetsPopup;
    private Popup? _runningAppsPopup;
    private double _pickerHue;
    private double _pickerSaturation;
    private double _pickerValue = 1;
    private bool _draggingWheel;
    private bool _draggingValue;
    private bool _updatingColorFromPicker;

    private const int PickerSize = 224;

    private static readonly IReadOnlyList<AppPreset> Presets = new List<AppPreset>
    {
        new("DEVELOPMENT", "JetBrains Rider", new[] { "rider64", "rider" }, "IDE", "◈", "#FFB84D"),
        new("DEVELOPMENT", "Visual Studio", new[] { "devenv" }, "IDE", "◇", "#9B7BFF"),
        new("DEVELOPMENT", "Visual Studio Code", new[] { "code" }, "EDITOR", "⌘", "#36A9E1"),
        new("DEVELOPMENT", "IntelliJ IDEA", new[] { "idea64", "idea" }, "IDE", "◆", "#FF5C93"),
        new("DEVELOPMENT", "PyCharm", new[] { "pycharm64", "pycharm" }, "IDE", "◆", "#60D394"),
        new("DEVELOPMENT", "WebStorm", new[] { "webstorm64", "webstorm" }, "IDE", "◆", "#42C7D9"),
        new("DEVELOPMENT", "CLion", new[] { "clion64", "clion" }, "IDE", "◆", "#D76BFF"),
        new("DEVELOPMENT", "Android Studio", new[] { "studio64", "studio" }, "IDE", "△", "#62D98B"),
        new("DEVELOPMENT", "Unreal Engine", new[] { "unrealeditor", "ue5editor", "ue4editor" }, "ENGINE", "✦", "#8E7DFF"),
        new("DEVELOPMENT", "Unity", new[] { "unity" }, "ENGINE", "◇", "#B8C0CC"),

        new("CREATIVE", "Blender", new[] { "blender" }, "3D", "◎", "#F58A45"),
        new("CREATIVE", "Adobe Photoshop", new[] { "photoshop" }, "DESIGN", "□", "#31A8FF"),
        new("CREATIVE", "Adobe Illustrator", new[] { "illustrator" }, "DESIGN", "◇", "#FF9A00"),
        new("CREATIVE", "Adobe Premiere Pro", new[] { "adobe premiere pro" }, "VIDEO", "▹", "#9999FF"),
        new("CREATIVE", "Adobe After Effects", new[] { "afterfx" }, "VIDEO", "✧", "#D291FF"),
        new("CREATIVE", "DaVinci Resolve", new[] { "resolve" }, "VIDEO", "◉", "#E95D6A"),
        new("CREATIVE", "Figma", new[] { "figma" }, "DESIGN", "◌", "#A879FF"),

        new("PRODUCTIVITY", "Obsidian", new[] { "obsidian" }, "NOTES", "◇", "#8B5CF6"),
        new("PRODUCTIVITY", "Notion", new[] { "notion" }, "NOTES", "□", "#D5D8DE"),

        new("COMMUNICATION", "Discord", new[] { "discord" }, "CHAT", "◉", "#5865F2"),
        new("COMMUNICATION", "Slack", new[] { "slack" }, "CHAT", "✣", "#E7A7D4"),
        new("COMMUNICATION", "Microsoft Teams", new[] { "ms-teams", "teams" }, "CHAT", "◈", "#7B83EB"),

        new("BROWSER", "Google Chrome", new[] { "chrome" }, "BROWSER", "◉", "#52B788"),
        new("BROWSER", "Microsoft Edge", new[] { "msedge" }, "BROWSER", "◉", "#35B8C5"),
        new("BROWSER", "Mozilla Firefox", new[] { "firefox" }, "BROWSER", "◉", "#FF7139"),

        new("PLATFORM", "Steam", new[] { "steam" }, "PLATFORM", "⌁", "#5C7FA3"),
        new("PLATFORM", "Spotify", new[] { "spotify" }, "MUSIC", "♪", "#1DB954"),
    };

    public TrackerApp? Result { get; private set; }

    public AppEditorWindow(TrackerApp? app)
    {
        _editing = app;
        Title = Localization.T(app is null ? "Editor.AddTitle" : "Editor.EditTitle");
        Width = 470;
        Height = 720;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrushFor("#151925");
        Foreground = BrushFor("#F2F4F8");
        FontFamily = new FontFamily("Segoe UI");

        _name.Text = app?.Name ?? string.Empty;
        _processes.Text = app is null ? string.Empty : string.Join(", ", app.ProcessNames);
        _type.Text = app?.Type ?? "PROGRAM";
        _icon.Text = app?.Icon ?? "◈";
        _description.Text = app is null
            ? Localization.T("Default.CustomDescription")
            : Localization.LocalizeStoredDescription(app.Description);
        _color.Text = app?.Color ?? "#29D3A2";
        _projectPath.Text = app?.ProjectPath ?? string.Empty;

        Content = BuildContent();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(28) };
        for (var i = 0; i < 10; i++) root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock { Text = Localization.T("Editor.Kicker"), Foreground = BrushFor("#5D6472"), FontFamily = new FontFamily("Consolas"), FontSize = 10 });
        var title = new TextBlock { Text = Title, FontSize = 25, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 22, 0, 0) };
        Grid.SetRow(title, 1);
        root.Children.Add(title);

        var lead = new TextBlock
        {
            Text = Localization.T("Editor.Lead"),
            Foreground = BrushFor("#8D94A4"),
            FontSize = 11,
            Margin = new Thickness(0, 8, 0, 19),
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(lead, 2);
        root.Children.Add(lead);

        AddField(root, Localization.T("Editor.FieldName"), _name, 3, Localization.T("Editor.ExampleName"));
        AddField(root, Localization.T("Editor.FieldProcesses"), BuildProcessesRow(), 4, string.Empty);
        AddField(root, Localization.T("Editor.FieldTypeIcon"), BuildTypeIconRow(), 5, string.Empty);
        AddField(root, Localization.T("Editor.FieldDescription"), _description, 6, Localization.T("Default.CustomDescription"));
        AddField(root, Localization.T("Editor.FieldColor"), BuildColorRow(), 7, string.Empty);
        AddField(root, Localization.T("Editor.FieldProject"), BuildProjectPathRow(), 8, string.Empty);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0),
        };
        var cancel = new Button
        {
            Content = Localization.T("Editor.Cancel"),
            Foreground = BrushFor("#8D94A4"),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(13, 9, 13, 9),
            Margin = new Thickness(0, 0, 8, 0),
        };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        var save = new Button
        {
            Content = Localization.T("Editor.Save"),
            Foreground = BrushFor("#07140F"),
            Background = BrushFor("#56E0B0"),
            BorderBrush = BrushFor("#56E0B0"),
            Padding = new Thickness(15, 9, 15, 9),
            FontWeight = FontWeights.Bold,
        };
        save.Click += Save_Click;
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        Grid.SetRow(buttons, 9);
        root.Children.Add(buttons);

        return root;
    }

    private StackPanel BuildTypeIconRow()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        _type.Width = 220;
        _icon.Width = 70;
        _icon.Margin = new Thickness(10, 0, 0, 0);
        StyleTextBox(_type);
        StyleTextBox(_icon);
        row.Children.Add(_type);
        row.Children.Add(_icon);
        return row;
    }

    private Grid BuildProcessesRow()
    {
        StyleTextBox(_processes, Localization.T("Editor.ExampleProcess"));

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _processes.Margin = new Thickness(0, 0, 8, 0);
        row.Children.Add(_processes);

        var runningAppsButton = new Button
        {
            Content = Localization.T("Editor.RunningApps"),
            ToolTip = Localization.T("Editor.RunningAppsHint"),
            Foreground = BrushFor("#DCE2EC"),
            Background = BrushFor("#252B38"),
            BorderBrush = BrushFor("#353B48"),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 8, 0),
        };
        runningAppsButton.Click += async (_, _) => await ShowRunningAppsAsync(runningAppsButton);
        Grid.SetColumn(runningAppsButton, 1);
        row.Children.Add(runningAppsButton);

        var presetsButton = new Button
        {
            Content = Localization.T("Editor.PopularApps"),
            ToolTip = Localization.T("Editor.PopularAppsHint"),
            Foreground = BrushFor("#DCE2EC"),
            Background = BrushFor("#252B38"),
            BorderBrush = BrushFor("#353B48"),
            Padding = new Thickness(12, 8, 12, 8),
        };
        _presetsPopup = BuildPresetsPopup(presetsButton);
        presetsButton.Click += (_, _) =>
        {
            if (_presetsPopup is null) return;
            if (_runningAppsPopup is not null) _runningAppsPopup.IsOpen = false;
            _presetsPopup.IsOpen = !_presetsPopup.IsOpen;
        };
        Grid.SetColumn(presetsButton, 2);
        row.Children.Add(presetsButton);
        return row;
    }

    private Popup BuildPresetsPopup(Button placementTarget)
    {
        var popup = BuildAppListPopup(placementTarget, out var items);

        string? previousGroup = null;
        foreach (var preset in Presets)
        {
            if (!string.Equals(previousGroup, preset.Group, StringComparison.Ordinal))
            {
                items.Children.Add(BuildPopupGroupHeading(
                    LocalizePresetGroup(preset.Group),
                    previousGroup is null));
                previousGroup = preset.Group;
            }

            var executableLabel = string.Join(", ", preset.ProcessNames.Select(name => name + ".exe"));
            var row = BuildPopupRow(
                BuildMenuRowHeader(
                    BuildTextBadge(preset.Icon, preset.Color),
                    $"{preset.Name}   \u00B7   {executableLabel}"));
            row.MouseLeftButtonUp += (_, e) =>
            {
                ApplyPreset(preset);
                popup.IsOpen = false;
                e.Handled = true;
            };
            items.Children.Add(row);
        }

        return popup;
    }

    private void ApplyPreset(AppPreset preset)
    {
        _name.Text = preset.Name;
        _processes.Text = string.Join(", ", preset.ProcessNames.Select(name => name + ".exe"));
        _type.Text = preset.Type;
        _icon.Text = preset.Icon;
        _description.Text = Localization.Language == Localization.Korean
            ? $"{preset.Name} 사용 시간"
            : $"{preset.Name} playtime";
        _color.Text = preset.Color;
    }

    private async Task ShowRunningAppsAsync(Button placementTarget)
    {
        var originalContent = placementTarget.Content;
        placementTarget.IsEnabled = false;
        placementTarget.Content = Localization.T("Editor.ScanningApps");
        IReadOnlyList<RunningAppChoice> choices;

        try
        {
            try
            {
                choices = await Task.Run(GetRunningAppChoices);
            }
            catch
            {
                choices = Array.Empty<RunningAppChoice>();
            }
        }
        finally
        {
            placementTarget.Content = originalContent;
            placementTarget.IsEnabled = true;
        }

        if (_presetsPopup is not null) _presetsPopup.IsOpen = false;
        if (_runningAppsPopup is not null) _runningAppsPopup.IsOpen = false;
        _runningAppsPopup = BuildRunningAppsPopup(placementTarget, choices);
        _runningAppsPopup.IsOpen = true;
    }

    private Popup BuildRunningAppsPopup(Button placementTarget, IReadOnlyList<RunningAppChoice> choices)
    {
        var popup = BuildAppListPopup(placementTarget, out var items);
        items.Children.Add(BuildPopupGroupHeading(Localization.T("Editor.RunningAppsGroup"), true));

        if (choices.Count == 0)
        {
            items.Children.Add(new TextBlock
            {
                Text = Localization.T("Editor.NoRunningApps"),
                Foreground = BrushFor("#8D94A4"),
                Background = Brushes.Transparent,
                Margin = new Thickness(12, 7, 12, 12),
                FontSize = 11,
            });
            return popup;
        }

        foreach (var choice in choices)
        {
            var row = BuildPopupRow(
                BuildMenuRowHeader(
                    BuildRunningAppIcon(choice),
                    $"{choice.DisplayName}   \u00B7   {choice.ProcessName}.exe"),
                BuildRunningAppToolTip(choice));
            row.MouseLeftButtonUp += (_, e) =>
            {
                ApplyRunningApp(choice);
                popup.IsOpen = false;
                e.Handled = true;
            };
            items.Children.Add(row);
        }

        return popup;
    }

    private void ApplyRunningApp(RunningAppChoice choice)
    {
        _name.Text = choice.DisplayName;
        _processes.Text = choice.ProcessName + ".exe";
        _type.Text = "PROGRAM";
        _icon.Text = FirstGlyph(choice.DisplayName);
        _description.Text = Localization.Language == Localization.Korean
            ? $"{choice.DisplayName} 사용 시간"
            : $"{choice.DisplayName} playtime";
        _color.Text = "#29D3A2";
    }

    private static Popup BuildAppListPopup(Button placementTarget, out StackPanel items)
    {
        items = new StackPanel
        {
            Background = BrushFor("#151925"),
        };

        var scrollViewer = new ScrollViewer
        {
            Content = items,
            Background = BrushFor("#151925"),
            BorderThickness = new Thickness(0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MinWidth = 360,
            MaxWidth = 620,
            MaxHeight = 500,
            PanningMode = PanningMode.VerticalOnly,
        };

        var surface = new Border
        {
            Background = BrushFor("#151925"),
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            SnapsToDevicePixels = true,
            Child = scrollViewer,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(0, 0, 0),
                BlurRadius = 14,
                ShadowDepth = 4,
                Opacity = 0.45,
            },
        };

        return new Popup
        {
            PlacementTarget = placementTarget,
            Placement = PlacementMode.Bottom,
            VerticalOffset = 4,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            Child = surface,
        };
    }

    private static TextBlock BuildPopupGroupHeading(string text, bool isFirst)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = BrushFor("#747D8D"),
            Background = Brushes.Transparent,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(12, isFirst ? 8 : 16, 12, 5),
        };
    }

    private static Border BuildPopupRow(UIElement content, string? toolTip = null)
    {
        var row = new Border
        {
            Background = BrushFor("#151925"),
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 7, 12, 7),
            Child = content,
            ToolTip = toolTip,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        row.MouseEnter += (_, _) => row.Background = BrushFor("#203B3A");
        row.MouseLeave += (_, _) => row.Background = BrushFor("#151925");
        return row;
    }

    private static IReadOnlyList<RunningAppChoice> GetRunningAppChoices()
    {
        var choices = new Dictionary<string, RunningAppChoice>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                if (process.MainWindowHandle == IntPtr.Zero) continue;

                var windowTitle = process.MainWindowTitle?.Trim();
                if (string.IsNullOrWhiteSpace(windowTitle)) continue;

                var processName = process.ProcessName.Trim();
                if (string.IsNullOrWhiteSpace(processName)
                    || IgnoredRunningProcessNames.Contains(processName)
                    || processName.Equals("DevPlaytime", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? executablePath = null;
                try { executablePath = process.MainModule?.FileName; }
                catch { }

                var displayName = GetExecutableDisplayName(executablePath, processName);
                var choice = new RunningAppChoice(
                    displayName,
                    processName,
                    executablePath,
                    windowTitle,
                    TryExtractExecutableIcon(executablePath));

                if (!choices.TryGetValue(processName, out var existing)
                    || (existing.Icon is null && choice.Icon is not null))
                {
                    choices[processName] = choice;
                }
            }
            catch
            {
                // Some system or elevated processes do not expose all properties.
            }
            finally
            {
                process.Dispose();
            }
        }

        return choices.Values
            .OrderBy(choice => choice.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string GetExecutableDisplayName(string? executablePath, string processName)
    {
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            try
            {
                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(executablePath);
                var productName = versionInfo.ProductName?.Trim();
                if (!string.IsNullOrWhiteSpace(productName)) return productName;

                var description = versionInfo.FileDescription?.Trim();
                if (!string.IsNullOrWhiteSpace(description)) return description;
            }
            catch { }
        }

        return processName;
    }

    private static ImageSource? TryExtractExecutableIcon(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !System.IO.File.Exists(executablePath)) return null;

        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(executablePath);
            if (icon is null) return null;

            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(24, 24));
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildRunningAppToolTip(RunningAppChoice choice)
    {
        return string.IsNullOrWhiteSpace(choice.ExecutablePath)
            ? choice.WindowTitle
            : $"{choice.WindowTitle}\n{choice.ExecutablePath}";
    }

    private static Border BuildTextBadge(string text, string color)
    {
        return new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(5),
            Background = BrushFor(color),
            Child = new TextBlock
            {
                Text = text,
                Foreground = BrushFor("#F8FBFF"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }

    private static UIElement BuildRunningAppIcon(RunningAppChoice choice)
    {
        return choice.Icon is null
            ? BuildTextBadge(FirstGlyph(choice.DisplayName), "#29D3A2")
            : new Image
            {
                Source = choice.Icon,
                Width = 18,
                Height = 18,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
    }

    private static StackPanel BuildMenuRowHeader(UIElement icon, string label)
    {
        var iconHost = new Grid
        {
            Width = 24,
            Height = 24,
            Margin = new Thickness(0, 0, 10, 0),
        };
        iconHost.Children.Add(icon);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(iconHost);
        row.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = BrushFor("#DCE2EC"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        return row;
    }

    private static string FirstGlyph(string value)
    {
        var trimmed = value.Trim();
        return string.IsNullOrEmpty(trimmed) ? "◉" : trimmed[..1].ToUpperInvariant();
    }

    private static string LocalizePresetGroup(string group) => (Localization.Language, group) switch
    {
        (Localization.Korean, "DEVELOPMENT") => "개발",
        (Localization.Korean, "CREATIVE") => "디자인 · 창작",
        (Localization.Korean, "PRODUCTIVITY") => "생산성",
        (Localization.Korean, "COMMUNICATION") => "커뮤니케이션",
        (Localization.Korean, "BROWSER") => "브라우저",
        (Localization.Korean, "PLATFORM") => "플랫폼",
        _ => group,
    };

    private Grid BuildColorRow()
    {
        StyleTextBox(_color, Localization.T("Editor.ColorPlaceholder"));
        SyncPickerFromText();
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _color.Margin = new Thickness(0, 0, 8, 0);
        row.Children.Add(_color);

        _colorPreview.Width = 38;
        _colorPreview.Height = 38;
        _colorPreview.Margin = new Thickness(0, 0, 8, 0);
        _colorPreview.CornerRadius = new CornerRadius(7);
        _colorPreview.BorderThickness = new Thickness(1);
        _colorPreview.BorderBrush = BrushFor("#465064");
        _colorPreview.Background = BrushFor(_color.Text);
        _colorPreview.ToolTip = Localization.T("Editor.CurrentColor");
        Grid.SetColumn(_colorPreview, 1);
        row.Children.Add(_colorPreview);

        var paletteButton = new Button
        {
            Content = Localization.T("Editor.Palette"),
            Foreground = BrushFor("#DCE2EC"),
            Background = BrushFor("#252B38"),
            BorderBrush = BrushFor("#353B48"),
            Padding = new Thickness(12, 8, 12, 8),
        };
        Grid.SetColumn(paletteButton, 2);
        row.Children.Add(paletteButton);

        _colorPickerPopup = BuildColorPickerPopup(paletteButton);
        paletteButton.Click += (_, _) => ToggleColorPicker();
        _colorPreview.MouseLeftButtonUp += (_, _) => ToggleColorPicker();
        _color.TextChanged += (_, _) => UpdateColorPreview();
        return row;
    }

    private Popup BuildColorPickerPopup(Button placementTarget)
    {
        var popup = new Popup
        {
            PlacementTarget = placementTarget,
            Placement = PlacementMode.Bottom,
            VerticalOffset = 7,
            StaysOpen = false,
            AllowsTransparency = true,
        };
        var header = new Grid { Margin = new Thickness(2, 0, 2, 10) };
        header.Children.Add(new TextBlock { Text = Localization.T("Editor.SelectColor"), Foreground = BrushFor("#AEB5C2"), FontSize = 11 });
        _pickerHexText = new TextBlock { Text = "#29D3A2", Foreground = BrushFor("#56E0B0"), FontFamily = new FontFamily("Consolas"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right };
        header.Children.Add(_pickerHexText);

        var pickerGrid = new Grid();
        pickerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PickerSize) });
        pickerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        pickerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });

        _wheelCanvas = BuildWheelCanvas();
        pickerGrid.Children.Add(_wheelCanvas);

        _valueCanvas = new Canvas { Width = 28, Height = PickerSize, Margin = new Thickness(0, 0, 0, 0) };
        _valueBarImage = new Image { Width = 18, Height = PickerSize, Source = CreateValueBarBitmap(18, PickerSize, _pickerHue, _pickerSaturation) };
        Canvas.SetLeft(_valueBarImage, 5);
        _valueCanvas.Children.Add(_valueBarImage);
        _valueMarker = new Border
        {
            Width = 28,
            Height = 5,
            Background = Brushes.White,
            BorderBrush = BrushFor("#0B0D13"),
            BorderThickness = new Thickness(1),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(_valueMarker, 0);
        _valueCanvas.Children.Add(_valueMarker);
        _valueCanvas.MouseLeftButtonDown += ValueCanvas_MouseLeftButtonDown;
        _valueCanvas.MouseMove += ValueCanvas_MouseMove;
        _valueCanvas.MouseLeftButtonUp += ValueCanvas_MouseLeftButtonUp;
        Grid.SetColumn(_valueCanvas, 1);
        pickerGrid.Children.Add(_valueCanvas);

        var content = new StackPanel();
        content.Children.Add(header);
        content.Children.Add(pickerGrid);
        popup.Child = new Border
        {
            Background = BrushFor("#151925"),
            BorderBrush = BrushFor("#353B48"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10),
            Child = content,
        };
        UpdatePickerVisuals();
        return popup;
    }

    private Canvas BuildWheelCanvas()
    {
        var canvas = new Canvas { Width = PickerSize, Height = PickerSize, Background = BrushFor("#0B0D13") };
        var wheel = new Image { Width = PickerSize, Height = PickerSize, Source = CreateColorWheelBitmap(PickerSize), IsHitTestVisible = false };
        canvas.Children.Add(wheel);
        _wheelMarker = new Border
        {
            Width = 13,
            Height = 13,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(7),
            IsHitTestVisible = false,
        };
        canvas.Children.Add(_wheelMarker);
        canvas.MouseLeftButtonDown += WheelCanvas_MouseLeftButtonDown;
        canvas.MouseMove += WheelCanvas_MouseMove;
        canvas.MouseLeftButtonUp += WheelCanvas_MouseLeftButtonUp;
        return canvas;
    }

    private void ToggleColorPicker()
    {
        if (_colorPickerPopup is null) return;
        if (!_colorPickerPopup.IsOpen)
        {
            SyncPickerFromText();
            UpdatePickerVisuals();
        }
        _colorPickerPopup.IsOpen = !_colorPickerPopup.IsOpen;
    }

    private void WheelCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _draggingWheel = true;
        _wheelCanvas?.CaptureMouse();
        SetWheelColor(e.GetPosition(_wheelCanvas));
        e.Handled = true;
    }

    private void WheelCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_draggingWheel && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) SetWheelColor(e.GetPosition(_wheelCanvas));
    }

    private void WheelCanvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _draggingWheel = false;
        _wheelCanvas?.ReleaseMouseCapture();
    }

    private void SetWheelColor(Point point)
    {
        var center = PickerSize / 2d;
        var dx = point.X - center;
        var dy = point.Y - center;
        var radius = PickerSize / 2d - 3;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance > radius)
        {
            dx = dx / distance * radius;
            dy = dy / distance * radius;
            distance = radius;
        }

        if (distance > 0.5) _pickerHue = (Math.Atan2(dy, dx) * 180d / Math.PI + 360d) % 360d;
        _pickerSaturation = Math.Clamp(distance / radius, 0, 1);
        ApplyPickerColor();
    }

    private void ValueCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _draggingValue = true;
        _valueCanvas?.CaptureMouse();
        SetValue(e.GetPosition(_valueCanvas));
        e.Handled = true;
    }

    private void ValueCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_draggingValue && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) SetValue(e.GetPosition(_valueCanvas));
    }

    private void ValueCanvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _draggingValue = false;
        _valueCanvas?.ReleaseMouseCapture();
    }

    private void SetValue(Point point)
    {
        _pickerValue = 1 - Math.Clamp(point.Y / (PickerSize - 1d), 0, 1);
        ApplyPickerColor();
    }

    private void ApplyPickerColor()
    {
        var color = HsvToColor(_pickerHue, _pickerSaturation, _pickerValue);
        _updatingColorFromPicker = true;
        try
        {
            _color.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            _colorPreview.Background = new SolidColorBrush(color);
        }
        finally
        {
            _updatingColorFromPicker = false;
        }
        UpdatePickerVisuals();
    }

    private void SyncPickerFromText()
    {
        if (!TryHexToHsv(_color.Text, out var hue, out var saturation, out var value)) return;
        _pickerHue = hue;
        _pickerSaturation = saturation;
        _pickerValue = value;
    }

    private void UpdatePickerVisuals()
    {
        var center = PickerSize / 2d;
        var radius = PickerSize / 2d - 3;
        var angle = _pickerHue * Math.PI / 180d;
        var markerX = center + Math.Cos(angle) * radius * _pickerSaturation;
        var markerY = center + Math.Sin(angle) * radius * _pickerSaturation;
        if (_wheelMarker is not null)
        {
            Canvas.SetLeft(_wheelMarker, markerX - _wheelMarker.Width / 2d);
            Canvas.SetTop(_wheelMarker, markerY - _wheelMarker.Height / 2d);
        }
        if (_valueMarker is not null) Canvas.SetTop(_valueMarker, (1 - _pickerValue) * (PickerSize - 1d) - _valueMarker.Height / 2d);
        if (_valueBarImage is not null) _valueBarImage.Source = CreateValueBarBitmap(18, PickerSize, _pickerHue, _pickerSaturation);
        if (_pickerHexText is not null)
        {
            var color = HsvToColor(_pickerHue, _pickerSaturation, _pickerValue);
            _pickerHexText.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }

    private static BitmapSource CreateColorWheelBitmap(int size)
    {
        var pixels = new byte[size * size * 4];
        var center = size / 2d;
        var radius = center - 1;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center + 0.5d;
                var dy = y - center + 0.5d;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                var index = (y * size + x) * 4;
                if (distance > radius)
                {
                    pixels[index + 3] = 0;
                    continue;
                }
                var hue = (Math.Atan2(dy, dx) * 180d / Math.PI + 360d) % 360d;
                var color = HsvToColor(hue, distance / radius, 1);
                pixels[index] = color.B;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.R;
                pixels[index + 3] = 255;
            }
        }
        return BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
    }

    private static BitmapSource CreateValueBarBitmap(int width, int height, double hue, double saturation)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var value = 1 - y / (height - 1d);
            var color = HsvToColor(hue, saturation, value);
            for (var x = 0; x < width; x++)
            {
                var index = (y * width + x) * 4;
                pixels[index] = color.B;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.R;
                pixels[index + 3] = 255;
            }
        }
        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
    }

    private static Color HsvToColor(double hue, double saturation, double value)
    {
        hue = (hue % 360d + 360d) % 360d;
        var chroma = value * saturation;
        var x = chroma * (1 - Math.Abs((hue / 60d % 2) - 1));
        var match = value - chroma;
        var (red, green, blue) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x),
        };
        return Color.FromRgb(ToByte(red + match), ToByte(green + match), ToByte(blue + match));
    }

    private static bool TryHexToHsv(string value, out double hue, out double saturation, out double brightness)
    {
        hue = 0;
        saturation = 0;
        brightness = 1;
        var hex = value.Trim();
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length != 6 || !hex.All(Uri.IsHexDigit)) return false;
        var color = Color.FromRgb(Convert.ToByte(hex[0..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16));
        var red = color.R / 255d;
        var green = color.G / 255d;
        var blue = color.B / 255d;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;
        brightness = max;
        saturation = max == 0 ? 0 : delta / max;
        if (delta == 0) hue = 0;
        else if (max == red) hue = 60 * (((green - blue) / delta) % 6);
        else if (max == green) hue = 60 * (((blue - red) / delta) + 2);
        else hue = 60 * (((red - green) / delta) + 4);
        if (hue < 0) hue += 360;
        return true;
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value * 255d), 0, 255);

    private void UpdateColorPreview()
    {
        _colorPreview.Background = BrushFor(_color.Text.Trim());
        if (_colorPickerPopup?.IsOpen == true && !_updatingColorFromPicker)
        {
            SyncPickerFromText();
            UpdatePickerVisuals();
        }
    }

    private Grid BuildProjectPathRow()
    {
        StyleTextBox(_projectPath, Localization.T("Editor.ProjectPlaceholder"));
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(_projectPath);
        var browse = new Button
        {
            Content = Localization.T("Editor.Browse"),
            Foreground = BrushFor("#DCE2EC"),
            Background = BrushFor("#252B38"),
            BorderBrush = BrushFor("#353B48"),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(8, 0, 0, 0),
        };
        browse.Click += BrowseProject_Click;
        Grid.SetColumn(browse, 1);
        row.Children.Add(browse);
        return row;
    }

    private void BrowseProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Localization.T("Editor.ProjectDialogTitle"),
            Filter = Localization.T("Editor.ProjectFilter"),
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) == true) _projectPath.Text = dialog.FileName;
    }

    private static void AddField(Grid root, string label, UIElement content, int row, string placeholder)
    {
        var stack = new StackPanel { Margin = new Thickness(0, row == 3 ? 0 : 11, 0, 0) };
        stack.Children.Add(new TextBlock { Text = label, Foreground = BrushFor("#AEB5C2"), FontSize = 11, Margin = new Thickness(0, 0, 0, 6) });
        if (content is Control control) StyleTextBox(control, placeholder);
        stack.Children.Add(content);
        Grid.SetRow(stack, row);
        root.Children.Add(stack);
    }

    private static void StyleTextBox(Control control, string? placeholder = null)
    {
        if (control is not TextBox box) return;
        box.Foreground = BrushFor("#F2F4F8");
        box.Background = BrushFor("#0B0D13");
        box.BorderBrush = BrushFor("#353B48");
        box.BorderThickness = new Thickness(1);
        box.Padding = new Thickness(10, 8, 10, 8);
        box.FontSize = 12;
        if (!string.IsNullOrWhiteSpace(placeholder)) box.ToolTip = placeholder;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = _name.Text.Trim();
        var processes = _processes.Text
            .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(process => !string.IsNullOrWhiteSpace(process))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var projectPath = _projectPath.Text.Trim().Trim('"');

        if (string.IsNullOrWhiteSpace(name) || processes.Count == 0)
        {
            MessageBox.Show(Localization.T("Editor.InputRequired"), Localization.T("Editor.InputTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            var extension = System.IO.Path.GetExtension(projectPath);
            if (!extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".uproject", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(Localization.T("Editor.ProjectExtension"), Localization.T("Editor.ProjectCheckTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!System.IO.File.Exists(projectPath))
            {
                MessageBox.Show(Localization.T("Editor.ProjectNotFound"), Localization.T("Editor.ProjectCheckTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            projectPath = System.IO.Path.GetFullPath(projectPath);
        }

        Result = new TrackerApp
        {
            Id = _editing?.Id ?? Guid.NewGuid().ToString("N"),
            Name = name,
            ProcessNames = processes,
            Type = string.IsNullOrWhiteSpace(_type.Text) ? "PROGRAM" : _type.Text.Trim(),
            Icon = string.IsNullOrWhiteSpace(_icon.Text) ? "◈" : _icon.Text.Trim(),
            Description = _description.Text.Trim(),
            Color = NormalizeColor(_color.Text),
            ProjectPath = string.IsNullOrWhiteSpace(projectPath) ? null : projectPath,
            Favorite = _editing?.Favorite ?? false,
        };
        DialogResult = true;
        Close();
    }

    private static string Normalize(string value) => System.IO.Path.GetFileNameWithoutExtension(value.Trim().Trim('"')).ToLowerInvariant();

    private static string NormalizeColor(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(value.Trim(), "^#[0-9a-fA-F]{6}$")
            ? value.Trim().ToUpperInvariant()
            : "#29D3A2";

    private static SolidColorBrush BrushFor(string hex)
    {
        try { return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; }
        catch { return new SolidColorBrush(Color.FromRgb(86, 224, 176)); }
    }

    private static readonly HashSet<string> IgnoredRunningProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "applicationframehost",
        "dwm",
        "explorer",
        "lockapp",
        "searchhost",
        "shellexperiencehost",
        "startmenuexperiencehost",
        "textinputhost",
    };

    private sealed record RunningAppChoice(
        string DisplayName,
        string ProcessName,
        string? ExecutablePath,
        string WindowTitle,
        ImageSource? Icon);

    private sealed record AppPreset(
        string Group,
        string Name,
        IReadOnlyList<string> ProcessNames,
        string Type,
        string Icon,
        string Color);
}
