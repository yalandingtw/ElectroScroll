namespace ElectroScroll.Models;

public sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public bool StartMinimized { get; set; }
    public bool BypassWithModifiers { get; set; } = true;
    public bool EnablePerformanceMode { get; set; } = true;
    public bool AutoBypassFullscreen { get; set; } = true;
    public bool AutoBypassKnownGames { get; set; } = true;
    public bool DiagnosticsChartEnabled { get; set; }
    public bool DiagnosticsLoggingEnabled { get; set; }
    public int DiagnosticsLogMaxBytes { get; set; } = 1_000_000;
    public UiLanguage Language { get; set; } = UiLanguage.English;
    public List<string> GameProcessNames { get; set; } =
    [
        "cs2",
        "csgo",
        "valorant",
        "r5apex",
        "FortniteClient-Win64-Shipping",
        "Overwatch",
        "LeagueClient",
        "League of Legends",
        "RobloxPlayerBeta",
        "Minecraft.Windows",
        "eldenring",
        "Cyberpunk2077"
    ];
    public List<AppProfile> Profiles { get; set; } = CreateDefaultProfiles();

    public AppProfile DefaultProfile => Profiles.First(profile => profile.Name == "Default");

    public static List<AppProfile> CreateDefaultProfiles()
    {
        return
        [
            new AppProfile
            {
                Name = "Default",
                ProcessNames = [],
                Tuning = ScrollTuning.Precise()
            },
            new AppProfile
            {
                Name = "Browser",
                ProcessNames = ["chrome", "msedge", "firefox", "brave", "vivaldi"],
                Tuning = ScrollTuning.WebPrecise()
            },
            new AppProfile
            {
                Name = "Codex Desktop",
                ProcessNames = ["Codex", "ChatGPT", "OpenAI"],
                Tuning = ScrollTuning.DesktopPrecise()
            },
            new AppProfile
            {
                Name = "Code",
                ProcessNames = ["Code", "devenv", "rider64", "notepad++"],
                Tuning = ScrollTuning.Precise()
            }
        ];
    }
}
