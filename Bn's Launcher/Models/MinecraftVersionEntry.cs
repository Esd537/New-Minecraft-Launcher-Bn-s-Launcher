using System;

namespace Bn_s_Launcher.Models;

public sealed class MinecraftVersionEntry
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public DateTimeOffset ReleaseTime { get; set; }

    public bool IsLatestRelease { get; set; }

    public bool IsLatestSnapshot { get; set; }

    public string Badge
    {
        get
        {
            if (IsLatestRelease)
            {
                return "latest release";
            }

            if (IsLatestSnapshot)
            {
                return "latest snapshot";
            }

            return Type;
        }
    }

    public string ReleaseLabel => ReleaseTime.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}
