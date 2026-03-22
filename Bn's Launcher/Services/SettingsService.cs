using System;
using System.IO;
using System.Text.Json;
using Bn_s_Launcher.Models;

namespace Bn_s_Launcher.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public string SettingsDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BnsLauncher");

    public string SettingsPath => Path.Combine(SettingsDirectory, "launcher-settings.json");

    public LauncherSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                var emptySettings = new LauncherSettings();
                emptySettings.EnsureDefaults();
                return emptySettings;
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<LauncherSettings>(json, SerializerOptions);

            if (settings is not null && settings.Profiles.Count > 0 && settings.Instances.Count > 0)
            {
                settings.EnsureDefaults();
                NormalizeSettings(settings);
                return settings;
            }

            var legacy = JsonSerializer.Deserialize<LegacyLauncherSettings>(json, SerializerOptions);
            return CreateFromLegacy(legacy);
        }
        catch
        {
            var fallbackSettings = new LauncherSettings();
            fallbackSettings.EnsureDefaults();
            return fallbackSettings;
        }
    }

    public void Save(LauncherSettings settings)
    {
        settings.EnsureDefaults();
        NormalizeSettings(settings);
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(SettingsPath, json);
    }

    private static LauncherSettings CreateFromLegacy(LegacyLauncherSettings? legacy)
    {
        var settings = new LauncherSettings
        {
            LastLaunchAt = legacy?.LastLaunchAt,
            LastStatus = string.IsNullOrWhiteSpace(legacy?.LastStatus)
                ? "Pronto para configurar."
                : legacy!.LastStatus
        };

        settings.Profiles.Add(new LauncherProfile
        {
            Name = "Perfil principal",
            PlayerName = legacy?.ProfileName ?? "SteveBn",
            GameArguments = NormalizeGameArguments(legacy?.GameArguments),
            JvmArguments = legacy?.JvmArguments ?? new LauncherProfile().JvmArguments,
            MemoryMb = legacy?.MemoryMb ?? 4096,
            CloseOnLaunch = legacy?.CloseOnLaunch ?? false,
            FullScreen = legacy?.FullScreen ?? false,
            AccentColor = legacy?.AccentColor ?? "#0F6BFF",
            BackgroundImagePath = legacy?.BackgroundImagePath ?? string.Empty
        });

        settings.Instances.Add(new LauncherInstance
        {
            Name = "Instancia principal",
            VersionId = string.IsNullOrWhiteSpace(legacy?.VersionLabel)
                ? LauncherInstance.DefaultVersionId
                : legacy!.VersionLabel,
            VersionType = "release",
            GameDirectory = string.IsNullOrWhiteSpace(legacy?.GameDirectory)
                ? LauncherSettings.DefaultGameDirectory
                : legacy!.GameDirectory,
            JavaPath = legacy?.JavaPath ?? string.Empty,
            Notes = "Instancia migrada do formato simples."
        });

        settings.EnsureDefaults();
        NormalizeSettings(settings);
        return settings;
    }

    private static void NormalizeSettings(LauncherSettings settings)
    {
        foreach (var profile in settings.Profiles)
        {
            profile.GameArguments = NormalizeGameArguments(profile.GameArguments);
            if (string.IsNullOrWhiteSpace(profile.AccountMode))
            {
                profile.AccountMode = LauncherProfile.AccountModeOffline;
            }

            if (string.IsNullOrWhiteSpace(profile.PlayerName))
            {
                profile.PlayerName = "SteveBn";
            }

            if (string.IsNullOrWhiteSpace(profile.ThemePreset))
            {
                profile.ThemePreset = LauncherProfile.ThemePresetBlue;
            }

            if (!string.Equals(profile.ThemePreset, LauncherProfile.ThemePresetCustom, StringComparison.OrdinalIgnoreCase))
            {
                profile.AccentColor = profile.ThemePreset switch
                {
                    LauncherProfile.ThemePresetWhite => "#FFFFFF",
                    LauncherProfile.ThemePresetRed => "#FF2B2B",
                    _ => "#0F6BFF"
                };
            }
        }

        foreach (var instance in settings.Instances)
        {
            if (string.IsNullOrWhiteSpace(instance.LoaderKind))
            {
                instance.LoaderKind = LauncherInstance.LoaderKindVanilla;
            }

            if (string.IsNullOrWhiteSpace(instance.GameDirectory))
            {
                instance.GameDirectory = LauncherSettings.BuildGameDirectory(instance.Name);
            }

            if (string.IsNullOrWhiteSpace(instance.PreparedVersionId))
            {
                instance.PreparedSignature = string.Empty;
                instance.PreparedAt = null;
            }
        }
    }

    private static string NormalizeGameArguments(string? rawArguments)
    {
        return string.Equals(rawArguments?.Trim(), "--username {player}", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : rawArguments?.Trim() ?? string.Empty;
    }

    private sealed class LegacyLauncherSettings
    {
        public string? ProfileName { get; set; }

        public string? VersionLabel { get; set; }

        public string? LaunchTargetPath { get; set; }

        public string? GameDirectory { get; set; }

        public string? JavaPath { get; set; }

        public string? GameArguments { get; set; }

        public string? JvmArguments { get; set; }

        public int? MemoryMb { get; set; }

        public bool? CloseOnLaunch { get; set; }

        public bool? FullScreen { get; set; }

        public string? AccentColor { get; set; }

        public string? BackgroundImagePath { get; set; }

        public DateTime? LastLaunchAt { get; set; }

        public string? LastStatus { get; set; }
    }
}
