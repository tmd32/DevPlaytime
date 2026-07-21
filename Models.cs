using System.Text.Json.Serialization;

namespace DevPlaytimeDesktop;

public sealed class AppStore
{
    public int Version { get; set; } = 4;
    public string Language { get; set; } = Localization.Language;
    public List<TrackerApp> Apps { get; set; } = new();
    public List<SessionRecord> Sessions { get; set; } = new();
    public DateTimeOffset? LastHeartbeatAt { get; set; }

    public static AppStore CreateDefault() => new()
    {
        Language = Localization.Language,
        Apps = new List<TrackerApp>(),
    };
}

public sealed class TrackerApp
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = Localization.T("General.Program");
    public string Type { get; set; } = "PROGRAM";
    public string Icon { get; set; } = "◈";
    public string Color { get; set; } = "#29D3A2";
    public string Description { get; set; } = Localization.T("Default.CustomDescription");
    public List<string> ProcessNames { get; set; } = new();
    public string? ProjectPath { get; set; }
    public bool Favorite { get; set; }
    public bool Archived { get; set; }
}

public sealed class SessionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AppId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? EndedAt { get; set; }
    public string Source { get; set; } = "process";
    public string? ProjectPath { get; set; }
    public string? EndReason { get; set; }
    public bool ProcessObserved { get; set; }
}
