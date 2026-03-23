using System;
namespace Bn_s_Launcher.Models;

public sealed class LauncherInstance
{
    public const string DefaultVersionId = "1.21.11";
    public const string LoaderKindVanilla = "vanilla";
    public const string LoaderKindForge = "forge";
    public const string LoaderKindOptifine = "optifine";
    public const string LoaderKindForgeOptifine = "forge+optifine";
    public const string GameDirectoryModeSeparate = "instancia separada";
    public const string GameDirectoryModeShared = ".minecraft padrao";
    public const string VersionSourceCatalog = "catalogo oficial";
    public const string VersionSourceCustom = "custom/local";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Instancia principal";

    public string VersionId { get; set; } = DefaultVersionId;

    public string VersionType { get; set; } = "release";

    public string VersionSource { get; set; } = VersionSourceCatalog;

    public string CustomVersionId { get; set; } = string.Empty;

    public string LoaderKind { get; set; } = LoaderKindVanilla;

    public string ForgeVersion { get; set; } = string.Empty;

    public string OptifineVersion { get; set; } = string.Empty;

    public string GameDirectory { get; set; } = LauncherSettings.DefaultGameDirectory;

    public string GameDirectoryMode { get; set; } = GameDirectoryModeSeparate;

    public string JavaPath { get; set; } = string.Empty;

    public bool KeepLauncherOpenWhilePlaying { get; set; } = true;

    public string Notes { get; set; } = "Instancia principal pronta para configurar.";

    public DateTime? LastPlayedAt { get; set; }

    public string PreparedSignature { get; set; } = string.Empty;

    public string PreparedVersionId { get; set; } = string.Empty;

    public DateTime? PreparedAt { get; set; }

    public bool UsesForge => LoaderKind == LoaderKindForge || LoaderKind == LoaderKindForgeOptifine;

    public bool UsesOptifine => LoaderKind == LoaderKindOptifine || LoaderKind == LoaderKindForgeOptifine;

    public bool UsesSharedMinecraftDirectory => GameDirectoryMode == GameDirectoryModeShared;

    public bool UsesCustomVersion => VersionSource == VersionSourceCustom && !string.IsNullOrWhiteSpace(CustomVersionId);

    public string RequestedVersionId => UsesCustomVersion ? CustomVersionId.Trim() : VersionId.Trim();

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
            VersionSource = VersionSource,
            CustomVersionId = CustomVersionId,
            LoaderKind = LoaderKind,
            ForgeVersion = ForgeVersion,
            OptifineVersion = OptifineVersion,
            GameDirectoryMode = GameDirectoryMode,
            GameDirectory = LauncherSettings.ResolveGameDirectory(name, GameDirectoryMode),
            JavaPath = JavaPath,
            KeepLauncherOpenWhilePlaying = KeepLauncherOpenWhilePlaying,
            Notes = Notes
        };
    }
}
