using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using Button = System.Windows.Controls.Button;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;

namespace DevPlaytimeDesktop;

public partial class MainWindow : Window
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ManualLaunchTimeout = TimeSpan.FromMinutes(2);
    private readonly string _dataFile;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly DispatcherTimer _scanTimer = new();
    private readonly Dictionary<string, double> _scrollOffsets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _autoStartSuppressed = new(StringComparer.OrdinalIgnoreCase);
    private AppStore _store;
    private List<RunningProcessInfo> _runningProcesses = new();
    private string _currentView = "library";
    private TextBlock _todayValue = null!;
    private TextBlock _weekValue = null!;
    private TextBlock _sessionValue = null!;
    private TextBlock _activeValue = null!;
    private TextBlock _activeCaption = null!;
    private Forms.NotifyIcon? _trayIcon;
    private Forms.ToolStripItem? _trayOpenItem;
    private Forms.ToolStripItem? _trayExitItem;
    private Drawing.Icon? _applicationIcon;
    private bool _allowExit;
    private bool _isClosing;
    private bool _scanInProgress;
    private bool _storeDirty;

    public MainWindow()
    {
        InitializeComponent();
        _dataFile = Environment.GetEnvironmentVariable("DEVPLAYTIME_DATA_FILE")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DevPlaytime", "devplaytime.json");
        _store = LoadStore();
        Localization.SetLanguage(_store.Language);
        UpdateStaticTexts();
        BuildSummaryCards();
        SetView("library");
        ConfigureTrayIcon();
        StateChanged += Window_StateChanged;
        Loaded += Window_Loaded;

        _scanTimer.Interval = TimeSpan.FromSeconds(5);
        _scanTimer.Tick += async (_, _) => await ScanAndRefreshAsync();
        _scanTimer.Start();
        _ = ScanAndRefreshAsync();
    }

    private void ConfigureTrayIcon()
    {
        _applicationIcon = LoadApplicationIcon();
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _applicationIcon,
            Text = Localization.T("Tray.Status"),
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();

        var menu = new Forms.ContextMenuStrip();
        _trayOpenItem = menu.Items.Add(Localization.T("Tray.Open"));
        _trayOpenItem.Click += (_, _) => ShowFromTray();
        menu.Items.Add(new Forms.ToolStripSeparator());
        _trayExitItem = menu.Items.Add(Localization.T("Tray.Exit"));
        _trayExitItem.Click += (_, _) => ExitApplication();
        _trayIcon.ContextMenuStrip = menu;
    }

    private void UpdateStaticTexts()
    {
        AutoTrackingTitle.Text = Localization.T("Static.AutoTrackingTitle");
        AutoTrackingDescription.Text = Localization.T("Static.AutoTrackingDescription");
        LocalTrackerText.Text = "  ·  " + Localization.T("Static.LocalTracker");
        WorkspaceText.Text = Localization.T("Static.Workspace");
        VersionText.Text = Localization.T("Static.LocalOnly");
        EyebrowText.Text = Localization.T("Static.LocalWorkspace");
        LibraryNav.Content = Localization.T("Nav.Library");
        TimelineNav.Content = Localization.T("Nav.Timeline");
        SettingsNav.Content = Localization.T("Nav.Settings");
        AddProgramButton.Content = Localization.T("Static.AddProgram");
        PrivacyText.Text = Localization.T("Static.Privacy");
        PageTitle.Text = PageTitleFor(_currentView);
        if (!_scanInProgress) ScanStatus.Text = Localization.T("Status.Detecting");
    }

    private void UpdateTrayTexts()
    {
        if (_trayIcon is not null) _trayIcon.Text = Localization.T("Tray.Status");
        if (_trayOpenItem is not null) _trayOpenItem.Text = Localization.T("Tray.Open");
        if (_trayExitItem is not null) _trayExitItem.Text = Localization.T("Tray.Exit");
    }

    private static Drawing.Icon LoadApplicationIcon()
    {
        try
        {
            var executable = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executable))
            {
                var icon = Drawing.Icon.ExtractAssociatedIcon(executable);
                if (icon is not null) return icon;
            }
        }
        catch
        {
            // Fall back to a cloned system icon if the executable icon cannot be read.
        }

        return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (Environment.GetCommandLineArgs().Any(argument => string.Equals(argument, "--background", StringComparison.OrdinalIgnoreCase)))
        {
            Dispatcher.BeginInvoke(new Action(HideToTray));
        }
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizeButton();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The window can receive a final mouse event while it is closing.
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleWindowState();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaximizeButton();
    }

    private void UpdateMaximizeButton()
    {
        if (MaximizeButton is not null) MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
    }

    private void HideToTray()
    {
        WindowState = WindowState.Normal;
        ShowInTaskbar = false;
        Hide();
    }

    private void ShowFromTray()
    {
        ActivateWindow();
    }

    internal void ActivateFromExternalLaunch()
    {
        ActivateWindow();
    }

    private void ActivateWindow()
    {
        if (_isClosing) return;

        ShowInTaskbar = true;
        if (!IsVisible) Show();
        WindowState = WindowState.Normal;

        Topmost = true;
        Activate();
        Topmost = false;
        Activate();
        Focus();
    }

    private void ExitApplication()
    {
        _allowExit = true;
        Close();
    }

    private AppStore LoadStore()
    {
        try
        {
            if (File.Exists(_dataFile))
            {
                var loaded = JsonSerializer.Deserialize<AppStore>(File.ReadAllText(_dataFile), _jsonOptions);
                if (loaded is not null)
                {
                    var previousVersion = loaded.Version;
                    var needsSave = previousVersion < 4;
                    loaded.Apps ??= new();
                    loaded.Sessions ??= new();
                    var normalizedLanguage = previousVersion < 4
                        ? Localization.Language
                        : Localization.NormalizeLanguage(loaded.Language);
                    if (!string.Equals(loaded.Language, normalizedLanguage, StringComparison.Ordinal))
                    {
                        loaded.Language = normalizedLanguage;
                        needsSave = true;
                    }
                    loaded.Version = Math.Max(loaded.Version, 4);
                    foreach (var app in loaded.Apps)
                    {
                        var processNamesWereNull = app.ProcessNames is null;
                        var existingNames = app.ProcessNames ?? new List<string>();
                        var normalizedNames = existingNames
                            .Select(NormalizeProcessName)
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        if (processNamesWereNull || !existingNames.SequenceEqual(normalizedNames, StringComparer.OrdinalIgnoreCase))
                        {
                            app.ProcessNames = normalizedNames;
                            needsSave = true;
                        }

                        var normalizedProjectPath = NormalizeProjectPath(app.ProjectPath);
                        if (!string.Equals(app.ProjectPath, normalizedProjectPath, StringComparison.Ordinal))
                        {
                            app.ProjectPath = normalizedProjectPath;
                            needsSave = true;
                        }
                    }

                    var now = DateTimeOffset.Now;
                    foreach (var session in loaded.Sessions)
                    {
                        var normalizedProjectPath = NormalizeProjectPath(session.ProjectPath);
                        if (!string.Equals(session.ProjectPath, normalizedProjectPath, StringComparison.Ordinal))
                        {
                            session.ProjectPath = normalizedProjectPath;
                            needsSave = true;
                        }

                        if (session.EndedAt is null)
                        {
                            var heartbeat = loaded.LastHeartbeatAt;
                            var recoveryEnd = heartbeat is not null && heartbeat >= session.StartedAt && heartbeat <= now
                                ? heartbeat.Value
                                : now;
                            session.EndedAt = recoveryEnd;
                            session.EndReason = "app_restart";
                            needsSave = true;
                        }
                    }

                    if (loaded.LastHeartbeatAt is not null)
                    {
                        loaded.LastHeartbeatAt = null;
                        needsSave = true;
                    }

                    if (needsSave) SaveStore(loaded);
                    return loaded;
                }
            }
        }
        catch
        {
            BackupCorruptStore();
        }

        var created = AppStore.CreateDefault();
        SaveStore(created);
        return created;
    }

    private void BackupCorruptStore()
    {
        try
        {
            if (!File.Exists(_dataFile)) return;
            var backupFile = $"{_dataFile}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.bak";
            File.Copy(_dataFile, backupFile, overwrite: false);
        }
        catch
        {
            // 원본 기록을 건드리지 못하는 환경에서는 새 저장 시도만 진행합니다.
        }
    }

    private bool SaveStore(AppStore? value = null)
    {
        var target = value ?? _store;
        try
        {
            var directory = Path.GetDirectoryName(_dataFile);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            var temporaryFile = _dataFile + ".tmp";
            File.WriteAllText(temporaryFile, JsonSerializer.Serialize(target, _jsonOptions));
            File.Move(temporaryFile, _dataFile, overwrite: true);
            _storeDirty = false;
            return true;
        }
        catch (Exception error)
        {
            _storeDirty = true;
            ScanStatus.Text = Localization.T("Status.SaveFailed", error.Message);
            ScanDot.Fill = BrushFor("#FF7878");
            return false;
        }
    }

    private void BuildSummaryCards()
    {
        SummaryGrid.Children.Clear();
        AddSummaryCard(Localization.T("Summary.Today"), "✦", true, out _todayValue, out _);
        AddSummaryCard(Localization.T("Summary.Week"), "◷", false, out _weekValue, out _);
        AddSummaryCard(Localization.T("Summary.Total"), "⌁", false, out _sessionValue, out _);
        AddSummaryCard(Localization.T("Summary.Active"), "●", false, out _activeValue, out _activeCaption);
    }

    private void AddSummaryCard(string title, string symbol, bool highlight, out TextBlock value, out TextBlock caption)
    {
        var border = new Border
        {
            Background = BrushFor(highlight ? "#173C37" : "#151925"),
            BorderBrush = BrushFor(highlight ? "#2B6C5F" : "#252B38"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 12, 0),
        };
        var stack = new StackPanel();
        var top = new Grid();
        top.Children.Add(new TextBlock { Text = title, Foreground = BrushFor("#8D94A4"), FontSize = 11 });
        top.Children.Add(new TextBlock { Text = symbol, Foreground = BrushFor(highlight ? "#56E0B0" : "#727B8D"), FontSize = 16, HorizontalAlignment = HorizontalAlignment.Right });
        stack.Children.Add(top);
        value = new TextBlock { Text = Localization.T("Duration.Zero"), FontSize = 28, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 23, 0, 7) };
        stack.Children.Add(value);
        caption = new TextBlock { Text = Localization.T("Summary.LocalCaption"), Foreground = BrushFor("#747D8D"), FontSize = 10 };
        stack.Children.Add(caption);
        border.Child = stack;
        SummaryGrid.Children.Add(border);
    }

    private async Task ScanAndRefreshAsync()
    {
        if (_isClosing || _scanInProgress) return;
        _scanInProgress = true;
        var repeatScan = false;
        try
        {
            var processNames = GetTrackedProcessNames();
            var runningProcesses = await Task.Run(() => GetRunningProcesses(processNames));
            if (_isClosing) return;
            if (!new HashSet<string>(processNames, StringComparer.OrdinalIgnoreCase).SetEquals(GetTrackedProcessNames()))
            {
                repeatScan = true;
                return;
            }
            _runningProcesses = runningProcesses;
            var changed = false;
            var now = DateTimeOffset.Now;

            foreach (var app in _store.Apps.Where(item => !item.Archived))
            {
                var projectPath = NormalizeProjectPath(app.ProjectPath);
                var active = ActiveSession(app.Id, projectPath);
                var trackingKey = TrackingKey(app.Id, projectPath);
                foreach (var stale in _store.Sessions.Where(item => item.AppId == app.Id && item.EndedAt is null && !SameProjectPath(item.ProjectPath, projectPath)).ToList())
                {
                    stale.EndedAt = now;
                    stale.EndReason = "tracking_target_changed";
                    changed = true;
                }

                var processRunning = IsProcessRunning(app, _runningProcesses);
                var projectState = string.IsNullOrWhiteSpace(projectPath)
                    ? processRunning ? ProjectRunState.Running : ProjectRunState.Stopped
                    : GetProjectRunState(app, projectPath, _runningProcesses);
                if (projectState == ProjectRunState.Stopped) _autoStartSuppressed.Remove(trackingKey);
                if (projectState == ProjectRunState.Running && active is null && !_autoStartSuppressed.Contains(trackingKey))
                {
                    _store.Sessions.Add(new SessionRecord { AppId = app.Id, ProjectPath = projectPath, StartedAt = now, Source = "process" });
                    changed = true;
                }
                else if (active?.Source == "manual" && !string.IsNullOrWhiteSpace(projectPath) && projectState == ProjectRunState.Running && !active.ProcessObserved)
                {
                    active.ProcessObserved = true;
                    changed = true;
                }
                else if (projectState == ProjectRunState.Stopped && active?.Source == "process")
                {
                    active.EndedAt = now;
                    active.EndReason = "process_exit";
                    changed = true;
                }
                else if (projectState == ProjectRunState.Stopped && active?.Source == "manual" && !string.IsNullOrWhiteSpace(projectPath) && active.ProcessObserved)
                {
                    active.EndedAt = now;
                    active.EndReason = "process_exit";
                    changed = true;
                }
                else if (projectState == ProjectRunState.Stopped
                    && active?.Source == "manual"
                    && !string.IsNullOrWhiteSpace(projectPath)
                    && !active.ProcessObserved
                    && now - active.StartedAt >= ManualLaunchTimeout)
                {
                    active.EndedAt = now;
                    active.EndReason = "launch_timeout";
                    changed = true;
                }
            }

            var hasActiveSessions = _store.Sessions.Any(item => item.EndedAt is null);
            if (hasActiveSessions)
            {
                var lastHeartbeat = _store.LastHeartbeatAt;
                if (lastHeartbeat is null || now < lastHeartbeat.Value || now - lastHeartbeat.Value >= HeartbeatInterval)
                {
                    _store.LastHeartbeatAt = now;
                    changed = true;
                }
            }
            else if (_store.LastHeartbeatAt is not null)
            {
                _store.LastHeartbeatAt = null;
                changed = true;
            }

            var saved = (!changed && !_storeDirty) || SaveStore();
            UpdateSummary();
            if (saved)
            {
                ScanDot.Fill = BrushFor("#56E0B0");
                ScanStatus.Text = Localization.T("Status.ScanReady", DateTime.Now.ToString("tt h:mm", Localization.Culture));
            }
            RefreshCurrentView();
        }
        catch (Exception error)
        {
            if (!_isClosing)
            {
                ScanDot.Fill = BrushFor("#FF7878");
                ScanStatus.Text = Localization.T("Status.ScanError", error.Message);
            }
        }
        finally
        {
            _scanInProgress = false;
            if (repeatScan && !_isClosing) _ = ScanAndRefreshAsync();
        }
    }

    private List<string> GetTrackedProcessNames() =>
        _store.Apps
            .Where(item => !item.Archived)
            .SelectMany(item => item.ProcessNames)
            .Select(NormalizeProcessName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<RunningProcessInfo> GetRunningProcesses(IEnumerable<string> processNames)
    {
        var names = processNames
            .Select(NormalizeProcessName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count == 0) return new();

        try
        {
            var clauses = names.Select(name => $"Name = '{EscapeWmiLiteral(name + ".exe")}'");
            using var searcher = new ManagementObjectSearcher($"SELECT Name, CommandLine FROM Win32_Process WHERE {string.Join(" OR ", clauses)}");
            using var processes = searcher.Get();
            return processes
                .Cast<ManagementObject>()
                .Select(process => new RunningProcessInfo(
                    NormalizeProcessName(process["Name"] as string ?? string.Empty),
                    process["CommandLine"] as string ?? string.Empty))
                .ToList();
        }
        catch
        {
            // WMI가 제한된 환경에서는 프로세스 이름만으로 기존 방식에 안전하게 폴백합니다.
            var result = new List<RunningProcessInfo>();
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    var name = NormalizeProcessName(process.ProcessName);
                    if (names.Contains(name, StringComparer.OrdinalIgnoreCase)) result.Add(new RunningProcessInfo(name, string.Empty));
                }
                catch { /* 권한이 없는 프로세스는 건너뜁니다. */ }
                finally { process.Dispose(); }
            }
            return result;
        }
    }

    private static bool IsProcessRunning(TrackerApp app, IReadOnlyCollection<RunningProcessInfo> processes) =>
        processes.Any(process => app.ProcessNames.Any(name => string.Equals(NormalizeProcessName(name), process.Name, StringComparison.OrdinalIgnoreCase)));

    private static ProjectRunState GetProjectRunState(TrackerApp app, string projectPath, IReadOnlyCollection<RunningProcessInfo> processes)
    {
        var targetPath = NormalizeCommandText(projectPath);
        var matchingProcesses = processes
            .Where(process => app.ProcessNames.Any(name => string.Equals(NormalizeProcessName(name), process.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (matchingProcesses.Count == 0) return ProjectRunState.Stopped;

        var hasUnknownCommandLine = false;
        foreach (var process in matchingProcesses)
        {
            if (string.IsNullOrWhiteSpace(process.CommandLine))
            {
                hasUnknownCommandLine = true;
                continue;
            }

            var commandLine = NormalizeCommandText(process.CommandLine);
            if (CommandLineContainsPath(commandLine, targetPath))
            {
                return ProjectRunState.Running;
            }
        }

        return hasUnknownCommandLine ? ProjectRunState.Unknown : ProjectRunState.Stopped;
    }

    private static bool CommandLineContainsPath(string commandLine, string targetPath)
    {
        var searchIndex = 0;
        while (searchIndex < commandLine.Length)
        {
            var matchIndex = commandLine.IndexOf(targetPath, searchIndex, StringComparison.OrdinalIgnoreCase);
            if (matchIndex < 0) return false;

            var endIndex = matchIndex + targetPath.Length;
            var hasStartBoundary = matchIndex == 0 || IsCommandLineBoundary(commandLine[matchIndex - 1], beforePath: true);
            var hasEndBoundary = endIndex == commandLine.Length || IsCommandLineBoundary(commandLine[endIndex], beforePath: false);
            if (hasStartBoundary && hasEndBoundary) return true;

            searchIndex = matchIndex + 1;
        }

        return false;
    }

    private static bool IsCommandLineBoundary(char value, bool beforePath) =>
        char.IsWhiteSpace(value)
        || value is '"' or '\'' or ',' or ';'
        || (beforePath && value == '=');

    private static string EscapeWmiLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static string NormalizeCommandText(string value) => value.Trim().Replace('/', '\\');

    private static string NormalizeProcessName(string value)
    {
        var name = Path.GetFileNameWithoutExtension(value.Trim().Trim('"'));
        return name.ToLowerInvariant();
    }

    private SessionRecord? ActiveSession(string appId) => ActiveSession(appId, null);

    private SessionRecord? ActiveSession(string appId, string? projectPath) =>
        _store.Sessions.FirstOrDefault(item => item.AppId == appId && item.EndedAt is null && SameProjectPath(item.ProjectPath, projectPath));

    private SessionRecord? ActiveSessionForApp(TrackerApp app) => ActiveSession(app.Id, NormalizeProjectPath(app.ProjectPath));

    private static string? NormalizeProjectPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try { return Path.GetFullPath(path.Trim().Trim('"')); }
        catch { return path.Trim().Trim('"'); }
    }

    private static bool SameProjectPath(string? left, string? right) =>
        string.Equals(NormalizeProjectPath(left), NormalizeProjectPath(right), StringComparison.OrdinalIgnoreCase);

    private static string TrackingKey(string appId, string? projectPath) =>
        $"{appId}\n{NormalizeProjectPath(projectPath) ?? string.Empty}";

    private void UpdateSummary()
    {
        var now = DateTimeOffset.Now;
        var todayStart = new DateTimeOffset(now.Date);
        var weekStart = todayStart.AddDays(-6);
        var activeApps = _store.Apps.Where(item => !item.Archived && ActiveSessionForApp(item) is not null).ToList();
        var todaySeconds = _store.Sessions.Sum(item => OverlapSeconds(item, todayStart, now));
        var weekSeconds = _store.Sessions.Sum(item => OverlapSeconds(item, weekStart, now));

        _todayValue.Text = FormatDuration(todaySeconds);
        _weekValue.Text = FormatDuration(weekSeconds);
        var totalSeconds = _store.Sessions.Sum(item => DurationSeconds(item, now));
        _sessionValue.Text = FormatDuration(totalSeconds);
        _activeValue.Text = Localization.T("Summary.ActiveCount", activeApps.Count);
        _activeCaption.Text = activeApps.Count == 0
            ? Localization.T("Summary.NoActive")
            : string.Join(", ", activeApps.Select(item => item.Name));
    }

    private static int DurationSeconds(SessionRecord session, DateTimeOffset now)
    {
        return Math.Max(0, (int)Math.Floor(((session.EndedAt ?? now) - session.StartedAt).TotalSeconds));
    }

    private static int OverlapSeconds(SessionRecord session, DateTimeOffset rangeStart, DateTimeOffset rangeEnd)
    {
        var start = session.StartedAt > rangeStart ? session.StartedAt : rangeStart;
        var sessionEnd = session.EndedAt ?? DateTimeOffset.Now;
        var end = sessionEnd < rangeEnd ? sessionEnd : rangeEnd;
        return end <= start ? 0 : Math.Max(0, (int)Math.Floor((end - start).TotalSeconds));
    }

    private static string FormatDuration(int seconds, bool compact = false)
    {
        var hours = seconds / 3600;
        var minutes = (seconds % 3600) / 60;
        if (compact)
        {
            if (hours > 0) return $"{hours}h {minutes}m";
            if (minutes > 0) return $"{minutes}m";
            return "<1m";
        }
        if (hours > 0) return Localization.T("Duration.HoursMinutes", hours, minutes);
        if (minutes > 0) return Localization.T("Duration.Minutes", minutes);
        return Localization.T("Duration.Zero");
    }

    private static string PageTitleFor(string view) => view switch
    {
        "timeline" => Localization.T("Page.Timeline"),
        "settings" => Localization.T("Page.Settings"),
        _ => Localization.T("Page.Library"),
    };

    private void SetView(string view)
    {
        SaveCurrentScrollOffset(_currentView);
        _currentView = view;
        PageTitle.Text = PageTitleFor(view);
        SetNavState(LibraryNav, view == "library");
        SetNavState(TimelineNav, view == "timeline");
        SetNavState(SettingsNav, view == "settings");
        RefreshCurrentView(captureCurrentOffset: false);
    }

    private void RefreshCurrentView(bool captureCurrentOffset = true)
    {
        if (captureCurrentOffset)
        {
            SaveCurrentScrollOffset(_currentView);
        }

        var nextContent = _currentView switch
        {
            "timeline" => BuildTimelineView(),
            "settings" => BuildSettingsView(),
            _ => BuildLibraryView(),
        };

        // Keep the outer ScrollViewer instance alive. Replacing it on every scan
        // can reset the offset again during WPF's following layout pass.
        if (ContentHost.Content is ScrollViewer currentScroll && nextContent is ScrollViewer nextScroll)
        {
            currentScroll.Content = nextScroll.Content;
        }
        else
        {
            ContentHost.Content = nextContent;
        }

        if (ContentHost.Content is ScrollViewer scroll)
        {
            var offset = _scrollOffsets.TryGetValue(_currentView, out var savedOffset) ? savedOffset : 0;
            void RestoreScrollOffset()
            {
                if (!_isClosing && ReferenceEquals(ContentHost.Content, scroll))
                {
                    scroll.ScrollToVerticalOffset(offset);
                }
            }

            // Apply once immediately and once after the new content has been measured.
            RestoreScrollOffset();
            scroll.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(RestoreScrollOffset));
        }
    }

    private void SaveCurrentScrollOffset(string view)
    {
        if (ContentHost.Content is ScrollViewer scroll)
        {
            _scrollOffsets[view] = scroll.VerticalOffset;
        }
    }

    private static void SetNavState(Button button, bool active)
    {
        button.Background = BrushFor(active ? "#18322F" : "#00000000");
        button.Foreground = BrushFor(active ? "#F2F4F8" : "#8D94A4");
        button.BorderBrush = active ? BrushFor("#56E0B0") : Brushes.Transparent;
        button.BorderThickness = active ? new Thickness(2, 0, 0, 0) : new Thickness(0);
    }

    private UIElement BuildLibraryView()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 8, 0) };
        var root = new StackPanel();
        var activeApps = _store.Apps.Where(item => !item.Archived && ActiveSessionForApp(item) is not null).ToList();
        if (activeApps.Count > 0) root.Children.Add(BuildActiveBanner(activeApps));

        root.Children.Add(SectionHeader(
            Localization.T("Library.Kicker"),
            Localization.T("Library.Title"),
            Localization.T(
                "Library.Note",
                _store.Apps.Count(item => !item.Archived),
                FormatDuration(_store.Sessions.Sum(item => DurationForApp(item.Id))))));
        var wrap = new WrapPanel();
        var apps = _store.Apps.Where(item => !item.Archived).ToList();
        if (apps.Count == 0)
        {
            wrap.Children.Add(new Border
            {
                Width = 500,
                Padding = new Thickness(30),
                BorderBrush = BrushFor("#252B38"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Child = new TextBlock { Text = Localization.T("Library.Empty"), Foreground = BrushFor("#8D94A4"), FontSize = 13, TextWrapping = TextWrapping.Wrap },
            });
        }
        else
        {
            foreach (var app in apps) wrap.Children.Add(BuildAppCard(app));
        }
        root.Children.Add(wrap);
        scroll.Content = root;
        return scroll;
    }

    private Border BuildActiveBanner(List<TrackerApp> apps)
    {
        var border = new Border { Background = BrushFor("#173C37"), BorderBrush = BrushFor("#2B6C5F"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(13), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 24) };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = Localization.T("Library.ActiveKicker"), Foreground = BrushFor("#A2EED7"), FontFamily = new FontFamily("Consolas"), FontSize = 10, Margin = new Thickness(0, 0, 0, 10) });
        foreach (var app in apps)
        {
            var session = ActiveSessionForApp(app)!;
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(9), Background = BrushFor(app.Color), Child = new TextBlock { Text = app.Icon, Foreground = BrushFor("#07140F"), FontSize = 19, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
            var info = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            info.Children.Add(new TextBlock { Text = app.Name, FontWeight = FontWeights.Bold, FontSize = 13 });
            info.Children.Add(new TextBlock { Text = TrackingLabel(app) + " · " + SessionSourceLabel(session), Foreground = BrushFor("#869A95"), FontSize = 10, Margin = new Thickness(0, 4, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(info, 1);
            row.Children.Add(info);
            var time = new TextBlock { Text = FormatDuration(DurationSeconds(session, DateTimeOffset.Now), true), Foreground = BrushFor("#56E0B0"), FontFamily = new FontFamily("Consolas"), FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(time, 2);
            row.Children.Add(time);
            stack.Children.Add(row);
        }
        border.Child = stack;
        return border;
    }

    private Border BuildAppCard(TrackerApp app)
    {
        var active = ActiveSessionForApp(app);
        var card = new Border { Width = 305, Margin = new Thickness(0, 0, 16, 16), Background = BrushFor("#11141D"), BorderBrush = BrushFor(active is not null ? "#3E8E78" : "#252B38"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(0) };
        var root = new StackPanel();
        var cover = new Border { Height = 145, Background = BrushFor(app.Color), CornerRadius = new CornerRadius(15, 15, 0, 0), Padding = new Thickness(16) };
        var coverGrid = new Grid();
        coverGrid.Children.Add(new TextBlock { Text = Localization.T("Library.TrackerCount", app.ProcessNames.Count), Foreground = BrushFor("#BFFFFFFF"), FontFamily = new FontFamily("Consolas"), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right });
        coverGrid.Children.Add(new TextBlock { Text = app.Icon, Foreground = BrushFor("#F8FBFF"), FontSize = 59, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Bottom });
        if (active is not null) coverGrid.Children.Add(new Border { Background = BrushFor("#56E0B0"), CornerRadius = new CornerRadius(4), Padding = new Thickness(7, 4, 7, 4), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Child = new TextBlock { Text = Localization.T("Library.ActiveBadge"), Foreground = BrushFor("#07140F"), FontFamily = new FontFamily("Consolas"), FontSize = 9 } });
        cover.Child = coverGrid;
        root.Children.Add(cover);

        var body = new StackPanel { Margin = new Thickness(18, 16, 18, 15) };
        body.Children.Add(new TextBlock { Text = app.Type, Foreground = BrushFor("#5D6472"), FontFamily = new FontFamily("Consolas"), FontSize = 10 });
        body.Children.Add(new TextBlock { Text = app.Name, FontSize = 19, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 5, 0, 0) });
        body.Children.Add(new TextBlock { Text = Localization.LocalizeStoredDescription(app.Description), Foreground = BrushFor("#8D94A4"), FontSize = 11, TextWrapping = TextWrapping.Wrap, Height = 32, Margin = new Thickness(0, 5, 0, 8) });
        body.Children.Add(new TextBlock { Text = TrackingLabel(app), Foreground = BrushFor("#56E0B0"), FontFamily = new FontFamily("Consolas"), FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = app.ProjectPath ?? Localization.T("Tracking.ProgramAll") });
        body.Children.Add(new Border { Height = 1, Background = BrushFor("#252B38"), Margin = new Thickness(0, 9, 0, 6) });
        var meta = new Grid();
        meta.Children.Add(new TextBlock { Text = Localization.T("Library.TotalPlaytime"), Foreground = BrushFor("#5D6472"), FontSize = 10 });
        meta.Children.Add(new TextBlock { Text = FormatDuration(DurationForApp(app), true), Foreground = BrushFor("#DCE2EC"), FontFamily = new FontFamily("Consolas"), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Right });
        body.Children.Add(meta);
        body.Children.Add(new Border { Height = 3, Background = BrushFor(app.Color), CornerRadius = new CornerRadius(3), Opacity = .8, Margin = new Thickness(0, 10, 0, 14) });
        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock { Text = (app.ProcessNames.FirstOrDefault() ?? "-") + ".exe", Foreground = BrushFor("#5D6472"), FontFamily = new FontFamily("Consolas"), FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
        var startLabel = string.IsNullOrWhiteSpace(app.ProjectPath)
            ? Localization.T("Tracking.ManualStart")
            : Localization.T("Tracking.ProjectLaunch");
        var action = new Button { Content = active?.Source == "process" ? Localization.T("Tracking.Automatic") : active is null ? startLabel : Localization.T("Tracking.StopSession"), Tag = app.Id, Style = (Style)FindResource("FlatButton"), Foreground = BrushFor(active?.Source == "process" ? "#5D6472" : "#56E0B0"), FontSize = 10, Padding = new Thickness(7, 4, 0, 4), IsEnabled = active?.Source != "process" };
        action.Click += Session_Click;
        Grid.SetColumn(action, 1);
        footer.Children.Add(action);
        body.Children.Add(footer);
        root.Children.Add(body);
        card.Child = root;
        return card;
    }

    private UIElement BuildTimelineView()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 8, 0) };
        var root = new StackPanel();
        root.Children.Add(SectionHeader(Localization.T("Timeline.Kicker"), Localization.T("Timeline.Title"), Localization.T("Timeline.Note")));
        var top = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.7, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var chart = CreatePanel();
        chart.Margin = new Thickness(0, 0, 8, 0);
        chart.Child = BuildChartPanel();
        Grid.SetColumn(chart, 0);
        top.Children.Add(chart);
        var quote = CreatePanel();
        quote.Margin = new Thickness(8, 0, 0, 0);
        quote.Background = BrushFor("#1C1A32");
        quote.Child = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "“", Foreground = BrushFor("#988BFF"), FontSize = 62, FontWeight = FontWeights.Bold },
                new TextBlock { Text = Localization.T("Timeline.Quote"), Foreground = BrushFor("#E4E1FF"), FontSize = 19, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 0) },
                new TextBlock { Text = Localization.T("Timeline.Logbook"), Foreground = BrushFor("#7771AD"), FontFamily = new FontFamily("Consolas"), FontSize = 10, Margin = new Thickness(0, 28, 0, 0) },
            },
        };
        Grid.SetColumn(quote, 1);
        top.Children.Add(quote);
        root.Children.Add(top);
        var recent = CreatePanel();
        recent.Child = BuildSessionList();
        root.Children.Add(recent);
        scroll.Content = root;
        return scroll;
    }

    private UIElement BuildChartPanel()
    {
        var root = new StackPanel();
        var header = new Grid();
        header.Children.Add(new StackPanel
        {
            Children =
            {
                new TextBlock { Text = Localization.T("Timeline.Last7DaysKicker"), Foreground = BrushFor("#5D6472"), FontFamily = new FontFamily("Consolas"), FontSize = 10 },
                new TextBlock { Text = Localization.T("Timeline.Daily"), FontSize = 17, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 5, 0, 0) },
            },
        });
        var now = DateTimeOffset.Now;
        var weekStart = new DateTimeOffset(now.Date).AddDays(-6);
        var days = Enumerable.Range(0, 7).Select(offset => weekStart.AddDays(offset)).ToList();
        var total = days.Sum(day => _store.Sessions.Sum(session => OverlapSeconds(session, day, day.AddDays(1))));
        header.Children.Add(new TextBlock { Text = FormatDuration(total), Foreground = BrushFor("#56E0B0"), FontFamily = new FontFamily("Consolas"), FontSize = 14, HorizontalAlignment = HorizontalAlignment.Right });
        root.Children.Add(header);
        var max = Math.Max(60, days.Max(day => _store.Sessions.Sum(session => OverlapSeconds(session, day, day.AddDays(1)))));
        var chart = new UniformGrid { Columns = 7, Height = 155, Margin = new Thickness(0, 22, 0, 0) };
        foreach (var day in days)
        {
            var seconds = _store.Sessions.Sum(session => OverlapSeconds(session, day, day.AddDays(1)));
            var column = new Grid();
            column.Children.Add(new Border { Width = 25, Height = Math.Max(4, (seconds / (double)max) * 115), Background = BrushFor(day.Date == now.Date ? "#FFBD65" : "#2C9A7A"), CornerRadius = new CornerRadius(5, 5, 2, 2), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center });
            column.Children.Add(new TextBlock { Text = $"{day.Month}/{day.Day}", Foreground = BrushFor(day.Date == now.Date ? "#FFBD65" : "#5D6472"), FontFamily = new FontFamily("Consolas"), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, -19) });
            chart.Children.Add(column);
        }
        root.Children.Add(chart);
        return root;
    }

    private UIElement BuildSessionList()
    {
        var root = new StackPanel();
        root.Children.Add(new StackPanel
        {
            Children =
            {
                new TextBlock { Text = Localization.T("Timeline.RecentKicker"), Foreground = BrushFor("#5D6472"), FontFamily = new FontFamily("Consolas"), FontSize = 10 },
                new TextBlock { Text = Localization.T("Timeline.Recent"), FontSize = 17, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 5, 0, 14) },
            },
        });
        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        AddColumns(header);
        AddCell(header, Localization.T("Timeline.Program"), 0, "#5D6472");
        AddCell(header, Localization.T("Timeline.Start"), 1, "#5D6472");
        AddCell(header, Localization.T("Timeline.End"), 2, "#5D6472");
        AddCell(header, Localization.T("Timeline.Source"), 3, "#5D6472");
        AddCell(header, Localization.T("Timeline.Duration"), 4, "#5D6472", true);
        root.Children.Add(header);

        var appById = _store.Apps
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var session in _store.Sessions.OrderByDescending(item => item.StartedAt).Take(40))
        {
            appById.TryGetValue(session.AppId, out var app);
            var row = new Grid { Margin = new Thickness(0, 7, 0, 7) };
            AddColumns(row);
            var sessionLabel = app is null ? Localization.T("Timeline.DeletedProgram") : app.Name + (string.IsNullOrWhiteSpace(session.ProjectPath) ? string.Empty : $" · {Path.GetFileNameWithoutExtension(session.ProjectPath)}");
            AddCell(row, sessionLabel, 0, "#F2F4F8", false, true);
            AddCell(row, session.StartedAt.LocalDateTime.ToString("M/d tt h:mm", Localization.Culture), 1, "#C8CED8");
            AddCell(row, session.EndedAt is null ? Localization.T("Timeline.InProgress") : session.EndedAt.Value.LocalDateTime.ToString("tt h:mm", Localization.Culture), 2, session.EndedAt is null ? "#56E0B0" : "#C8CED8");
            AddCell(row, SessionSourceLabel(session, compact: true), 3, "#5D6472");
            AddCell(row, FormatDuration(DurationSeconds(session, DateTimeOffset.Now), true), 4, "#C8CED8", true);
            root.Children.Add(row);
        }
        if (_store.Sessions.Count == 0) root.Children.Add(new TextBlock { Text = Localization.T("Timeline.Empty"), Foreground = BrushFor("#5D6472"), FontSize = 11, Margin = new Thickness(0, 10, 0, 10) });
        return root;
    }

    private UIElement BuildSettingsView()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(.82, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });

        var intro = CreatePanel();
        intro.Margin = new Thickness(0, 0, 8, 0);
        intro.Background = BrushFor("#152A2A");
        intro.Child = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "⌁", Foreground = BrushFor("#56E0B0"), FontSize = 42, Margin = new Thickness(0, 0, 0, 35) },
                new TextBlock { Text = Localization.T("Settings.HowItWorks"), Foreground = BrushFor("#5D6472"), FontFamily = new FontFamily("Consolas"), FontSize = 10 },
                new TextBlock { Text = Localization.T("Settings.IntroTitle"), FontSize = 26, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 15) },
                new TextBlock { Text = Localization.T("Settings.IntroDescription"), Foreground = BrushFor("#8D94A4"), FontSize = 11, TextWrapping = TextWrapping.Wrap, LineHeight = 18 },
                new Border { Background = BrushFor("#10221F"), CornerRadius = new CornerRadius(9), Padding = new Thickness(12), Margin = new Thickness(0, 22, 0, 0), Child = new TextBlock { Text = Localization.T("Settings.Example"), Foreground = BrushFor("#B9DCD1"), FontFamily = new FontFamily("Consolas"), FontSize = 10, LineHeight = 16 } },
            },
        };
        Grid.SetColumn(intro, 0);
        grid.Children.Add(intro);

        var listPanel = CreatePanel();
        listPanel.Margin = new Thickness(8, 0, 0, 0);
        var listRoot = new StackPanel();
        var listHeader = new Grid();
        listHeader.Children.Add(new StackPanel
        {
            Children =
            {
                new TextBlock { Text = Localization.T("Settings.YourApps"), Foreground = BrushFor("#5D6472"), FontFamily = new FontFamily("Consolas"), FontSize = 10 },
                new TextBlock { Text = Localization.T("Settings.AppsTitle"), FontSize = 17, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 5, 0, 0) },
            },
        });
        var add = new Button { Content = Localization.T("Action.Add"), Style = (Style)FindResource("FlatButton"), Foreground = BrushFor("#56E0B0"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
        add.Click += AddApp_Click;
        listHeader.Children.Add(add);
        listRoot.Children.Add(listHeader);
        listRoot.Children.Add(BuildLanguageSelector());
        foreach (var app in _store.Apps.Where(item => !item.Archived)) listRoot.Children.Add(BuildSettingsRow(app));
        listPanel.Child = listRoot;
        Grid.SetColumn(listPanel, 1);
        grid.Children.Add(listPanel);
        return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 8, 0), Content = grid };
    }

    private Border BuildLanguageSelector()
    {
        var panel = new Border
        {
            Background = BrushFor("#161A25"),
            BorderBrush = BrushFor("#252B38"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(13),
            Margin = new Thickness(0, 18, 0, 0),
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new StackPanel
        {
            Children =
            {
                new TextBlock { Text = Localization.T("Settings.LanguageTitle"), FontWeight = FontWeights.Bold, FontSize = 12 },
                new TextBlock { Text = Localization.T("Settings.LanguageDescription"), Foreground = BrushFor("#5D6472"), FontSize = 10, Margin = new Thickness(0, 3, 0, 0) },
            },
        });

        var choices = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        choices.Children.Add(CreateLanguageButton(Localization.T("Settings.Korean"), Localization.Korean));
        choices.Children.Add(CreateLanguageButton(Localization.T("Settings.English"), Localization.English));
        Grid.SetColumn(choices, 1);
        grid.Children.Add(choices);
        panel.Child = grid;
        return panel;
    }

    private Button CreateLanguageButton(string label, string language)
    {
        var selected = string.Equals(Localization.Language, language, StringComparison.Ordinal);
        var button = new Button
        {
            Content = label,
            Tag = language,
            Style = (Style)FindResource("FlatButton"),
            Foreground = BrushFor(selected ? "#07140F" : "#8D94A4"),
            Background = BrushFor(selected ? "#56E0B0" : "#00000000"),
            BorderBrush = BrushFor(selected ? "#56E0B0" : "#353B48"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(6, 0, 0, 0),
        };
        button.Click += Language_Click;
        return button;
    }

    private void Language_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string language) return;
        var normalized = Localization.NormalizeLanguage(language);
        if (string.Equals(Localization.Language, normalized, StringComparison.Ordinal)) return;

        Localization.SetLanguage(normalized);
        _store.Language = normalized;
        SaveStore();
        UpdateStaticTexts();
        UpdateTrayTexts();
        BuildSummaryCards();
        SetView(_currentView);
        UpdateSummary();
        _ = ScanAndRefreshAsync();
    }

    private Border BuildSettingsRow(TrackerApp app)
    {
        var row = new Border { Background = BrushFor("#161A25"), BorderBrush = BrushFor("#252B38"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11), Padding = new Thickness(13), Margin = new Thickness(0, 18, 0, 0) };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var info = new StackPanel { Orientation = Orientation.Horizontal };
        info.Children.Add(new Border { Width = 31, Height = 31, CornerRadius = new CornerRadius(9), Background = BrushFor(app.Color), Child = new TextBlock { Text = app.Icon, Foreground = BrushFor("#07140F"), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
        info.Children.Add(new StackPanel { Margin = new Thickness(11, 0, 0, 0), Children = { new TextBlock { Text = app.Name, FontWeight = FontWeights.Bold, FontSize = 12 }, new TextBlock { Text = TrackingLabel(app), Foreground = BrushFor("#56E0B0"), FontFamily = new FontFamily("Consolas"), FontSize = 10, Margin = new Thickness(0, 3, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis }, new TextBlock { Text = string.Join(" · ", app.ProcessNames.Select(name => name + ".exe")), Foreground = BrushFor("#5D6472"), FontFamily = new FontFamily("Consolas"), FontSize = 10, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis } } });
        grid.Children.Add(info);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var edit = new Button { Content = Localization.T("Action.Edit"), Tag = app.Id, Style = (Style)FindResource("FlatButton"), Foreground = BrushFor("#8D94A4"), FontSize = 11 };
        edit.Click += EditApp_Click;
        var delete = new Button { Content = Localization.T("Action.Delete"), Tag = app.Id, Style = (Style)FindResource("FlatButton"), Foreground = BrushFor("#FF7878"), FontSize = 11 };
        delete.Click += DeleteApp_Click;
        actions.Children.Add(edit);
        actions.Children.Add(delete);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);
        row.Child = grid;
        return row;
    }

    private static StackPanel SectionHeader(string kicker, string title, string note)
    {
        var root = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        var line = new Grid();
        line.Children.Add(new StackPanel
        {
            Children =
            {
                new TextBlock { Text = kicker, Foreground = BrushFor("#5D6472"), FontFamily = new FontFamily("Consolas"), FontSize = 10 },
                new TextBlock { Text = title, FontSize = 23, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 0) },
            },
        });
        line.Children.Add(new TextBlock { Text = note, Foreground = BrushFor("#5D6472"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom });
        root.Children.Add(line);
        return root;
    }

    private Border CreatePanel() => new() { Background = BrushFor("#11141D"), BorderBrush = BrushFor("#252B38"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(21) };

    private int DurationForApp(string appId) =>
        _store.Sessions.Where(item => item.AppId == appId).Sum(item => DurationSeconds(item, DateTimeOffset.Now));

    private int DurationForApp(TrackerApp app)
    {
        var projectPath = NormalizeProjectPath(app.ProjectPath);
        return _store.Sessions
            .Where(item => item.AppId == app.Id && (string.IsNullOrWhiteSpace(projectPath) || SameProjectPath(item.ProjectPath, projectPath)))
            .Sum(item => DurationSeconds(item, DateTimeOffset.Now));
    }

    private static string TrackingLabel(TrackerApp app) =>
        string.IsNullOrWhiteSpace(app.ProjectPath)
            ? Localization.T("Tracking.ProgramAll")
            : Localization.T("Tracking.Project", Path.GetFileName(app.ProjectPath));

    private static string SessionSourceLabel(SessionRecord session, bool compact = false)
    {
        if (session.Source != "manual") return Localization.T(compact ? "Session.AutomaticCompact" : "Session.Automatic");
        if (!string.IsNullOrWhiteSpace(session.ProjectPath)) return Localization.T(compact ? "Session.ProjectLaunchCompact" : "Session.ProjectLaunch");
        return Localization.T(compact ? "Session.ManualCompact" : "Session.Manual");
    }

    private void Session_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string appId) return;
        var app = _store.Apps.FirstOrDefault(item => item.Id == appId);
        if (app is null) return;
        var projectPath = NormalizeProjectPath(app.ProjectPath);
        var trackingKey = TrackingKey(appId, projectPath);
        var active = ActiveSession(appId, projectPath);
        if (active is null)
        {
            if (!TryOpenProjectFile(projectPath)) return;
            _autoStartSuppressed.Remove(trackingKey);
            _store.Sessions.Add(new SessionRecord { AppId = appId, ProjectPath = projectPath, StartedAt = DateTimeOffset.Now, Source = "manual" });
        }
        else if (active.Source == "manual")
        {
            active.EndedAt = DateTimeOffset.Now;
            active.EndReason = "manual_stop";
            _autoStartSuppressed.Add(trackingKey);
        }
        SaveStore();
        _ = ScanAndRefreshAsync();
    }

    private static bool TryOpenProjectFile(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath)) return true;
        if (!File.Exists(projectPath))
        {
            MessageBox.Show(
                Localization.T("Message.ProjectMissing", projectPath),
                Localization.T("Message.ProjectMissingTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = projectPath,
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception error)
        {
            MessageBox.Show(
                Localization.T("Message.ProjectOpenFailed", error.Message),
                Localization.T("Message.ProjectMissingTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
    }

    private void AddApp_Click(object sender, RoutedEventArgs e) => OpenEditor(null);

    private void EditApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string id) OpenEditor(_store.Apps.FirstOrDefault(item => item.Id == id));
    }

    private void DeleteApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;
        var app = _store.Apps.FirstOrDefault(item => item.Id == id);
        if (app is null) return;
        if (MessageBox.Show(
                Localization.T("Message.DeleteConfirm", app.Name),
                Localization.T("Message.DeleteTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        foreach (var active in _store.Sessions.Where(item => item.AppId == app.Id && item.EndedAt is null))
        {
            active.EndedAt = DateTimeOffset.Now;
            active.EndReason = "app_removed";
        }
        _store.Apps.Remove(app);
        SaveStore();
        _ = ScanAndRefreshAsync();
    }

    private void OpenEditor(TrackerApp? app)
    {
        var editor = new AppEditorWindow(app) { Owner = this };
        if (editor.ShowDialog() != true || editor.Result is null) return;
        var existing = _store.Apps.FirstOrDefault(item => item.Id == editor.Result.Id);
        if (existing is null) _store.Apps.Add(editor.Result);
        else CopyApp(existing, editor.Result);
        SaveStore();
        _ = ScanAndRefreshAsync();
    }

    private static void CopyApp(TrackerApp target, TrackerApp source)
    {
        target.Name = source.Name;
        target.Type = source.Type;
        target.Icon = source.Icon;
        target.Color = source.Color;
        target.Description = source.Description;
        target.ProcessNames = source.ProcessNames;
        target.ProjectPath = source.ProjectPath;
        target.Favorite = source.Favorite;
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string view) SetView(view);
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowExit)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        if (_isClosing) return;
        _isClosing = true;
        _scanTimer.Stop();
        foreach (var session in _store.Sessions.Where(item => item.EndedAt is null))
        {
            session.EndedAt = DateTimeOffset.Now;
            session.EndReason = "app_close";
        }
        _store.LastHeartbeatAt = null;
        SaveStore();
        _trayIcon?.Dispose();
        _applicationIcon?.Dispose();
    }

    private static void AddColumns(Grid grid)
    {
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(.7, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(.65, GridUnitType.Star) });
    }

    private static void AddCell(Grid grid, string text, int column, string color, bool right = false, bool bold = false)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = BrushFor(color),
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }

    private enum ProjectRunState
    {
        Stopped,
        Running,
        Unknown,
    }

    private sealed record RunningProcessInfo(string Name, string CommandLine);

    private static SolidColorBrush BrushFor(string hex)
    {
        try { return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; }
        catch { return new SolidColorBrush(Color.FromRgb(86, 224, 176)); }
    }
}
