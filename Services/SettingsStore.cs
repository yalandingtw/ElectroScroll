using System.IO;
using System.Text.Json;
using ElectroScroll.Models;

namespace ElectroScroll.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string SettingsPath { get; }

    public SettingsStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ElectroScroll");

        Directory.CreateDirectory(directory);
        SettingsPath = Path.Combine(directory, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return Normalize(settings ?? new AppSettings());
        }
        catch
        {
            TryBackupInvalidSettings();
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(Normalize(settings), JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.Profiles ??= [];
        settings.Profiles.RemoveAll(profile => profile is null);

        var defaultProfile = settings.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.Name, "Default", StringComparison.OrdinalIgnoreCase));
        if (defaultProfile is null)
        {
            settings.Profiles.Insert(0, new AppProfile { Name = "Default", Tuning = ScrollTuning.Precise() });
        }
        else
        {
            defaultProfile.Name = "Default";
            defaultProfile.ProcessNames ??= [];
            defaultProfile.Tuning ??= ScrollTuning.Precise();
        }

        EnsureProfile(settings, new AppProfile
        {
            Name = "Browser",
            ProcessNames = ["chrome", "msedge", "firefox", "brave", "vivaldi"],
            Tuning = ScrollTuning.WebPrecise()
        });
        EnsureProfile(settings, new AppProfile
        {
            Name = "Codex Desktop",
            ProcessNames = ["Codex", "ChatGPT", "OpenAI"],
            Tuning = ScrollTuning.DesktopPrecise()
        });
        EnsureProfile(settings, new AppProfile
        {
            Name = "Code",
            ProcessNames = ["Code", "devenv", "rider64", "notepad++"],
            Tuning = ScrollTuning.Precise()
        });

        foreach (var profile in settings.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                profile.Name = "Unnamed";
            }

            profile.Tuning ??= ScrollTuning.Balanced();
            profile.ProcessNames ??= [];
        }

        settings.GameProcessNames ??= [.. new AppSettings().GameProcessNames];

        return settings;
    }

    private static void EnsureProfile(AppSettings settings, AppProfile profile)
    {
        var existing = settings.Profiles.FirstOrDefault(existing =>
            string.Equals(existing.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Name = profile.Name;
            existing.ProcessNames ??= profile.ProcessNames;
            existing.Tuning ??= profile.Tuning;
            return;
        }

        var defaultIndex = settings.Profiles.FindIndex(existing => existing.Name == "Default");
        var insertIndex = defaultIndex >= 0 ? defaultIndex + 1 : Math.Min(1, settings.Profiles.Count);
        settings.Profiles.Insert(insertIndex, profile);
    }

    private void TryBackupInvalidSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return;
            }

            var backupPath = $"{SettingsPath}.invalid-{DateTime.Now:yyyyMMddHHmmss}";
            File.Copy(SettingsPath, backupPath, overwrite: false);
        }
        catch
        {
            // Loading must still recover even if the backup cannot be written.
        }
    }
}
