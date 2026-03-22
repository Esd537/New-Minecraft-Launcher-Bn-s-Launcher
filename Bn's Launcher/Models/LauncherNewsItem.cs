namespace Bn_s_Launcher.Models;

public sealed class LauncherNewsItem
{
    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Meta { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string ActionLabel { get; set; } = "Abrir";
}
