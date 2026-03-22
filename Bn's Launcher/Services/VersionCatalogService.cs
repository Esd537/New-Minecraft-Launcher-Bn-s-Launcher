using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bn_s_Launcher.Models;

namespace Bn_s_Launcher.Services;

public sealed class VersionCatalogService
{
    private const string ManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task<IReadOnlyList<MinecraftVersionEntry>> GetVersionsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await HttpClient.GetAsync(ManifestUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var latest = document.RootElement.GetProperty("latest");
        var latestRelease = latest.GetProperty("release").GetString() ?? string.Empty;
        var latestSnapshot = latest.GetProperty("snapshot").GetString() ?? string.Empty;

        var versions = new List<MinecraftVersionEntry>();

        foreach (var version in document.RootElement.GetProperty("versions").EnumerateArray())
        {
            versions.Add(new MinecraftVersionEntry
            {
                Id = version.GetProperty("id").GetString() ?? string.Empty,
                Type = version.GetProperty("type").GetString() ?? "release",
                ReleaseTime = version.GetProperty("releaseTime").GetDateTimeOffset(),
                IsLatestRelease = string.Equals(version.GetProperty("id").GetString(), latestRelease, StringComparison.OrdinalIgnoreCase),
                IsLatestSnapshot = string.Equals(version.GetProperty("id").GetString(), latestSnapshot, StringComparison.OrdinalIgnoreCase)
            });
        }

        return versions
            .OrderByDescending(version => version.ReleaseTime)
            .ToList();
    }
}
