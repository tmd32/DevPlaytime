using System.Globalization;

namespace DevPlaytimeDesktop;

public static class Localization
{
    public const string Korean = "ko";
    public const string English = "en";

    private static string _language = English;

    public static string Language => _language;

    public static CultureInfo Culture => _language == English
        ? CultureInfo.GetCultureInfo("en-US")
        : CultureInfo.GetCultureInfo("ko-KR");

    public static void SetLanguage(string? language) => _language = NormalizeLanguage(language);

    public static string NormalizeLanguage(string? language) =>
        language?.Trim().StartsWith(Korean, StringComparison.OrdinalIgnoreCase) == true ? Korean : English;

    public static string T(string key, params object[] arguments)
    {
        if (!Strings.TryGetValue(key, out var value)) return key;
        var text = _language == English ? value.English : value.Korean;
        return arguments.Length == 0 ? text : string.Format(Culture, text, arguments);
    }

    public static string LocalizeStoredDescription(string? description)
    {
        var value = description ?? string.Empty;
        return value switch
        {
            "코드와 에디터가 만나는 곳" or "Where code meets the editor" => T("Default.RiderDescription"),
            "월드를 만드는 시간" or "Time spent building worlds" => T("Default.UnrealDescription"),
            "나만의 작업 플레이타임" or "My personal work playtime" => T("Default.CustomDescription"),
            _ => value,
        };
    }

    private static readonly Dictionary<string, LocalizedText> Strings = new(StringComparer.Ordinal)
    {
        ["General.Program"] = new("프로그램", "Program"),
        ["Default.RiderDescription"] = new("코드와 에디터가 만나는 곳", "Where code meets the editor"),
        ["Default.UnrealDescription"] = new("월드를 만드는 시간", "Time spent building worlds"),
        ["Default.CustomDescription"] = new("나만의 작업 플레이타임", "My personal work playtime"),

        ["App.AlreadyRunningActivationFailed"] = new(
            "DevPlaytime이 이미 실행 중이지만 기존 창을 찾지 못했습니다. 작업표시줄이나 시스템 트레이를 확인해 주세요.",
            "DevPlaytime is already running, but its window could not be activated. Check the taskbar or system tray."),

        ["Static.AutoTrackingTitle"] = new("자동 추적 활성", "Automatic tracking active"),
        ["Static.AutoTrackingDescription"] = new("프로그램이 켜지는 순간\n작업 세션을 기록합니다.", "Work sessions are recorded\nas soon as a program starts."),
        ["Static.LocalTracker"] = new("로컬 추적기", "LOCAL TRACKER"),
        ["Static.Workspace"] = new("작업 공간", "WORKSPACE"),
        ["Static.LocalWorkspace"] = new("로컬 작업 공간", "LOCAL WORKSPACE"),
        ["Static.LocalOnly"] = new("로컬 전용  ·  v0.5.0", "LOCAL ONLY  ·  v0.5.0"),
        ["Static.AddProgram"] = new("＋ 프로그램 추가", "＋ Add program"),
        ["Static.Privacy"] = new(
            "DevPlaytime은 작업을 감시하지 않습니다. 단지 켜져 있던 시간을 조용히 기억합니다.",
            "DevPlaytime does not inspect your work. It quietly remembers how long programs stayed open."),

        ["Nav.Library"] = new("▦   라이브러리", "▦   Library"),
        ["Nav.Timeline"] = new("◷   타임라인", "◷   Timeline"),
        ["Nav.Settings"] = new("⚙   설정", "⚙   Settings"),
        ["Page.Library"] = new("내 작업 라이브러리", "My work library"),
        ["Page.Timeline"] = new("작업 타임라인", "Work timeline"),
        ["Page.Settings"] = new("추적 설정", "Tracking settings"),

        ["Tray.Status"] = new("DevPlaytime · 백그라운드 추적 중", "DevPlaytime · Tracking in the background"),
        ["Tray.Open"] = new("DevPlaytime 열기", "Open DevPlaytime"),
        ["Tray.Exit"] = new("완전히 종료", "Exit DevPlaytime"),

        ["Status.Detecting"] = new("프로그램 감지 준비 중", "Preparing program detection"),
        ["Status.SaveFailed"] = new("저장 실패: {0}", "Save failed: {0}"),
        ["Status.ScanReady"] = new("5초마다 자동 감지 · {0}", "Auto-detect every 5 seconds · {0}"),
        ["Status.ScanError"] = new("프로세스 감지 오류 · {0}", "Process detection error · {0}"),

        ["Summary.Today"] = new("오늘의 플레이타임", "Today's playtime"),
        ["Summary.Week"] = new("최근 7일", "Last 7 days"),
        ["Summary.Total"] = new("전체 플레이타임", "Total playtime"),
        ["Summary.Active"] = new("지금 작업 중", "Working now"),
        ["Summary.LocalCaption"] = new("현재 로컬 기록 기준", "Based on local records"),
        ["Summary.ActiveCount"] = new("{0}개", "{0}"),
        ["Summary.NoActive"] = new("감지된 프로그램 없음", "No detected programs"),

        ["Duration.HoursMinutes"] = new("{0}시간 {1}분", "{0}h {1}m"),
        ["Duration.Minutes"] = new("{0}분", "{0}m"),
        ["Duration.Zero"] = new("0분", "0m"),

        ["Library.Title"] = new("작업 라이브러리", "Work library"),
        ["Library.Kicker"] = new("내 컬렉션", "YOUR COLLECTION"),
        ["Library.ActiveKicker"] = new("●  작업 중", "●  NOW PLAYING"),
        ["Library.ActiveBadge"] = new("● 작업 중", "● NOW PLAYING"),
        ["Library.TrackerCount"] = new("{0:00} 추적 항목", "{0:00} TRACKER"),
        ["Library.Note"] = new("{0}개 프로그램 · 누적 {1}", "{0} programs · {1} total"),
        ["Library.Empty"] = new(
            "아직 등록한 프로그램이 없습니다.\n오른쪽 위의 ‘프로그램 추가’를 눌러 첫 작업을 등록해보세요.",
            "No programs have been added yet.\nSelect ‘Add program’ in the upper-right to create your first tracker."),
        ["Library.TotalPlaytime"] = new("총 플레이타임", "Total playtime"),

        ["Tracking.ProgramAll"] = new("프로그램 전체", "Entire program"),
        ["Tracking.Project"] = new("프로젝트 · {0}", "Project · {0}"),
        ["Tracking.ManualStart"] = new("수동 시작", "Start manually"),
        ["Tracking.ProjectLaunch"] = new("프로젝트 실행", "Launch project"),
        ["Tracking.Automatic"] = new("자동 추적 중", "Auto tracking"),
        ["Tracking.StopSession"] = new("세션 중지", "Stop session"),

        ["Timeline.Title"] = new("타임라인", "Timeline"),
        ["Timeline.Kicker"] = new("세션 기록", "SESSION HISTORY"),
        ["Timeline.Last7DaysKicker"] = new("최근 7일", "LAST 7 DAYS"),
        ["Timeline.Logbook"] = new("DEVPLAYTIME 기록장", "DEVPLAYTIME LOGBOOK"),
        ["Timeline.RecentKicker"] = new("최근 활동", "RECENT ACTIVITY"),
        ["Timeline.Note"] = new("최근 세션 기록", "Recent session history"),
        ["Timeline.Quote"] = new("작업도 플레이처럼.\n오늘의 한 시간을 저장하세요.", "Treat work like play.\nSave an hour from today."),
        ["Timeline.Daily"] = new("일별 플레이타임", "Daily playtime"),
        ["Timeline.Recent"] = new("최근 활동", "Recent activity"),
        ["Timeline.Program"] = new("프로그램", "Program"),
        ["Timeline.Start"] = new("시작", "Start"),
        ["Timeline.End"] = new("종료", "End"),
        ["Timeline.Source"] = new("방식", "Source"),
        ["Timeline.Duration"] = new("시간", "Duration"),
        ["Timeline.DeletedProgram"] = new("삭제된 프로그램", "Deleted program"),
        ["Timeline.InProgress"] = new("진행 중", "In progress"),
        ["Timeline.Empty"] = new(
            "아직 기록된 세션이 없습니다. Rider나 Unreal Editor를 실행해보세요.",
            "No sessions have been recorded yet. Try launching Rider or Unreal Editor."),

        ["Settings.IntroTitle"] = new("프로그램과\n프로젝트를 연결하세요.", "Connect programs\nand projects."),
        ["Settings.HowItWorks"] = new("작동 방식", "HOW IT WORKS"),
        ["Settings.YourApps"] = new("내 프로그램", "YOUR APPS"),
        ["Settings.IntroDescription"] = new(
            "실행 파일만 등록하면 프로그램 전체 시간을 기록합니다. .sln, .slnx 또는 .uproject를 연결하면 해당 프로젝트가 열린 경우에만 시간을 기록해요.",
            "Register an executable to track the entire program, or connect a .sln, .slnx, or .uproject file to track only that project."),
        ["Settings.Example"] = new("예시\nRider64.exe + MyGame.sln\nUnrealEditor.exe + MyGame.uproject", "EXAMPLE\nRider64.exe + MyGame.sln\nUnrealEditor.exe + MyGame.uproject"),
        ["Settings.AppsTitle"] = new("추적 중인 프로그램", "Tracked programs"),
        ["Settings.LanguageTitle"] = new("언어", "Language"),
        ["Settings.LanguageDescription"] = new("앱에 표시할 언어를 선택하세요.", "Choose the language used throughout the app."),
        ["Settings.Korean"] = new("한국어", "한국어"),
        ["Settings.English"] = new("English", "English"),
        ["Action.Add"] = new("＋ 추가", "＋ Add"),
        ["Action.Edit"] = new("편집", "Edit"),
        ["Action.Delete"] = new("삭제", "Delete"),

        ["Session.Automatic"] = new("프로세스 자동 추적", "Automatic process tracking"),
        ["Session.AutomaticCompact"] = new("자동", "Auto"),
        ["Session.ProjectLaunch"] = new("프로젝트 실행", "Project launch"),
        ["Session.ProjectLaunchCompact"] = new("실행", "Launch"),
        ["Session.Manual"] = new("수동 세션", "Manual session"),
        ["Session.ManualCompact"] = new("수동", "Manual"),

        ["Message.ProjectMissingTitle"] = new("프로젝트 열기 실패", "Project launch failed"),
        ["Message.ProjectMissing"] = new(
            "프로젝트 파일을 찾을 수 없습니다.\n\n{0}\n\n설정에서 .sln, .slnx 또는 .uproject 파일을 다시 선택해 주세요.",
            "The project file could not be found.\n\n{0}\n\nSelect the .sln, .slnx, or .uproject file again in Settings."),
        ["Message.ProjectOpenFailed"] = new(
            "프로젝트 파일을 연결 프로그램으로 열지 못했습니다.\n\n{0}",
            "The project file could not be opened with its associated program.\n\n{0}"),
        ["Message.DeleteTitle"] = new("프로그램 삭제", "Delete program"),
        ["Message.DeleteConfirm"] = new(
            "‘{0}’을(를) 라이브러리에서 삭제할까요?\n기존 시간 기록은 남습니다.",
            "Remove ‘{0}’ from the library?\nExisting time records will be kept."),

        ["Editor.AddTitle"] = new("프로그램 추가", "Add program"),
        ["Editor.EditTitle"] = new("프로그램 편집", "Edit program"),
        ["Editor.Kicker"] = new("사용자 추적 항목", "CUSTOM TRACKER"),
        ["Editor.Lead"] = new(
            "실행 파일 이름을 등록하면 프로그램이 켜질 때 자동으로 기록합니다. 프로젝트 파일을 연결하면 해당 .sln, .slnx 또는 .uproject가 열린 경우에만 프로젝트 시간을 기록합니다.",
            "Register an executable name to track the program automatically. Connect a project file to record time only while that .sln, .slnx, or .uproject is open."),
        ["Editor.FieldName"] = new("프로그램 이름", "Program name"),
        ["Editor.FieldProcesses"] = new("실행 파일 이름 · 쉼표로 여러 개 입력", "Executable names · separate multiple names with commas"),
        ["Editor.PopularApps"] = new("인기 앱", "Popular apps"),
        ["Editor.PopularAppsHint"] = new(
            "많이 사용하는 프로그램을 선택하면 이름과 실행 파일 정보가 자동으로 입력됩니다.",
            "Choose a popular app to fill in its name and executable information automatically."),
        ["Editor.RunningApps"] = new("실행 중 앱", "Running apps"),
        ["Editor.RunningAppsHint"] = new(
            "현재 창이 열려 있는 앱에서 선택합니다. 앱 아이콘은 실행 파일에서 자동으로 가져옵니다.",
            "Choose an app with an open window. Its icon is extracted automatically from the executable."),
        ["Editor.ScanningApps"] = new("검색 중...", "Scanning..."),
        ["Editor.RunningAppsGroup"] = new("현재 실행 중", "RUNNING NOW"),
        ["Editor.NoRunningApps"] = new("선택할 수 있는 실행 중 앱이 없습니다.", "No running apps are available to select."),
        ["Editor.FieldTypeIcon"] = new("분류 / 아이콘", "Category / icon"),
        ["Editor.FieldDescription"] = new("한 줄 설명", "Short description"),
        ["Editor.FieldColor"] = new("포인트 컬러", "Accent color"),
        ["Editor.FieldProject"] = new("프로젝트 파일 (선택)", "Project file (optional)"),
        ["Editor.ExampleName"] = new("예: Blender", "Example: Blender"),
        ["Editor.ExampleProcess"] = new("예: blender.exe", "Example: blender.exe"),
        ["Editor.Cancel"] = new("취소", "Cancel"),
        ["Editor.Save"] = new("저장하기", "Save"),
        ["Editor.ColorPlaceholder"] = new("HEX 색상 예: #29D3A2", "HEX color, e.g. #29D3A2"),
        ["Editor.CurrentColor"] = new("현재 선택한 색상", "Currently selected color"),
        ["Editor.Palette"] = new("팔레트", "Palette"),
        ["Editor.SelectColor"] = new("색상 선택", "Select color"),
        ["Editor.ProjectPlaceholder"] = new("예: C:\\Work\\MyGame\\MyGame.uproject 또는 MyApp.slnx", "Example: C:\\Work\\MyGame\\MyGame.uproject or MyApp.slnx"),
        ["Editor.Browse"] = new("찾아보기", "Browse"),
        ["Editor.ProjectDialogTitle"] = new("프로젝트 파일 선택", "Select a project file"),
        ["Editor.ProjectFilter"] = new(
            "프로젝트 파일 (*.sln;*.slnx;*.uproject)|*.sln;*.slnx;*.uproject|Visual Studio 솔루션 (*.sln;*.slnx)|*.sln;*.slnx|Unreal 프로젝트 (*.uproject)|*.uproject",
            "Project files (*.sln;*.slnx;*.uproject)|*.sln;*.slnx;*.uproject|Visual Studio solutions (*.sln;*.slnx)|*.sln;*.slnx|Unreal projects (*.uproject)|*.uproject"),
        ["Editor.InputTitle"] = new("입력 확인", "Check input"),
        ["Editor.InputRequired"] = new("프로그램 이름과 실행 파일 이름을 입력하세요.", "Enter a program name and at least one executable name."),
        ["Editor.ProjectCheckTitle"] = new("프로젝트 파일 확인", "Check project file"),
        ["Editor.ProjectExtension"] = new("프로젝트 파일은 .sln, .slnx 또는 .uproject만 연결할 수 있습니다.", "Only .sln, .slnx, and .uproject files can be connected."),
        ["Editor.ProjectNotFound"] = new("선택한 프로젝트 파일을 찾을 수 없습니다.", "The selected project file could not be found."),
    };

    private readonly record struct LocalizedText(string Korean, string English);
}
