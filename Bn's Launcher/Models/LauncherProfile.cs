using System;

namespace Bn_s_Launcher.Models;

public sealed class LauncherProfile
{
    public const string AccountModeOffline = "offline";

    public const string AccountModeMicrosoft = "microsoft";

    public const string ThemePresetBlue = "preto + azul";

    public const string ThemePresetWhite = "preto + branco";

    public const string ThemePresetRed = "preto + vermelho";

    public const string ThemePresetCustom = "custom";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Perfil principal";

    public string PlayerName { get; set; } = "SteveBn";

    public string AccountMode { get; set; } = AccountModeOffline;

    public string MicrosoftAccountName { get; set; } = string.Empty;

    public string GameArguments { get; set; } = string.Empty;

    public string JvmArguments { get; set; } =
        "-XX:+UseG1GC -XX:+ParallelRefProcEnabled -XX:MaxGCPauseMillis=50 " +
        "-XX:+UnlockExperimentalVMOptions -XX:+DisableExplicitGC " +
        "-XX:G1NewSizePercent=20 -XX:G1ReservePercent=20 " +
        "-XX:InitiatingHeapOccupancyPercent=15";

    public int MemoryMb { get; set; } = 4096;

    public bool CloseOnLaunch { get; set; }

    public bool FullScreen { get; set; }

    public string ThemePreset { get; set; } = ThemePresetBlue;

    public string AccentColor { get; set; } = "#0F6BFF";

    public string BackgroundImagePath { get; set; } = string.Empty;

    public string Detail => $"{GetDisplayName()} | {MemoryMb} MB | {(AccountMode == AccountModeMicrosoft ? "microsoft" : "offline")}";

    public string GetDisplayName()
    {
        return AccountMode == AccountModeMicrosoft && !string.IsNullOrWhiteSpace(MicrosoftAccountName)
            ? MicrosoftAccountName
            : PlayerName;
    }

    public LauncherProfile Clone(string name)
    {
        return new LauncherProfile
        {
            Name = name,
            PlayerName = PlayerName,
            AccountMode = AccountMode,
            MicrosoftAccountName = MicrosoftAccountName,
            GameArguments = GameArguments,
            JvmArguments = JvmArguments,
            MemoryMb = MemoryMb,
            CloseOnLaunch = CloseOnLaunch,
            FullScreen = FullScreen,
            ThemePreset = ThemePreset,
            AccentColor = AccentColor,
            BackgroundImagePath = BackgroundImagePath
        };
    }
}
