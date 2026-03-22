using System;
namespace Bn_s_Launcher.Models;

public sealed class LauncherInstance
{
    public const string DefaultVersionId = "1.21.11";
    public const string LoaderKindVanilla = "vanilla";
    public const string LoaderKindForge = "forge";
    public const string LoaderKindOptifine = "optifine";
    public const string LoaderKindForgeOptifine = "forge+optifine";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Instancia principal";

    public string VersionId { get; set; } = DefaultVersionId;

    public string VersionType { get; set; } = "release";

    public string LoaderKind { get; set; } = LoaderKindVanilla;

    public string ForgeVersion { get; set; } = string.Empty;

    public string OptifineVersion { get; set; } = string.Empty;

    public string GameDirectory { get; set; } = LauncherSettings.DefaultGameDirectory;

    public string JavaPath { get; set; } = string.Empty;

    public bool KeepLauncherOpenWhilePlaying { get; set; } = true;

    public string Notes { get; set; } = "Instancia principal pronta para configurar.";

    public DateTime? LastPlayedAt { get; set; }

    public string PreparedSignature { get; set; } = string.Empty;

    public string PreparedVersionId { get; set; } = string.Empty;

    public DateTime? PreparedAt { get; set; }

    public bool UsesForge => LoaderKind == LoaderKindForge || LoaderKind == LoaderKindForgeOptifine;

    public bool UsesOptifine => LoaderKind == LoaderKindOptifine || LoaderKind == LoaderKindForgeOptifine;

    public string LoaderDisplayName
    {
        get
        {
            return LoaderKind switch
            {
                LoaderKindForge => "Forge",
                LoaderKindOptifine => "OptiFine",
                LoaderKindForgeOptifine => "Forge + OptiFine",
                _ => "Vanilla"
            };
        }
    }

    public string Detail
    {
        get
        {
            return $"{LoaderDisplayName} | {VersionId} | {VersionType}";
        }
    }

    public LauncherInstance Clone(string name)
    {
        return new LauncherInstance
        {
            Name = name,
            VersionId = VersionId,
            VersionType = VersionType,
            LoaderKind = LoaderKind,
            ForgeVersion = ForgeVersion,
            OptifineVersion = OptifineVersion,
            GameDirectory = LauncherSettings.BuildGameDirectory(name),
            JavaPath = JavaPath,
            KeepLauncherOpenWhilePlaying = KeepLauncherOpenWhilePlaying,
            Notes = Notes
        };
    }
}
