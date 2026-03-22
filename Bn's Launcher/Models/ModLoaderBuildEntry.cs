namespace Bn_s_Launcher.Models;

public sealed class ModLoaderBuildEntry
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Meta { get; set; } = string.Empty;

    public string ForgeCompatibilityVersion { get; set; } = string.Empty;

    public bool IsRecommended { get; set; }

    public bool IsLatest { get; set; }

    public bool IsPreview { get; set; }
}
