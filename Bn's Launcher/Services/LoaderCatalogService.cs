using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Bn_s_Launcher.Models;
using CmlLib.Core.Installer.Forge.Versions;
using Optifine.Installer;

namespace Bn_s_Launcher.Services;

public sealed class LoaderCatalogService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public async Task<IReadOnlyList<ModLoaderBuildEntry>> GetForgeVersionsAsync(string minecraftVersion)
    {
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return [];
        }

        var loader = new ForgeVersionLoader(HttpClient);
        var versions = await loader.GetForgeVersions(minecraftVersion.Trim());

        return versions
            .Select(version => new ModLoaderBuildEntry
            {
                Id = version.ForgeVersionName,
                DisplayName = BuildForgeDisplayName(version),
                Meta = string.IsNullOrWhiteSpace(version.Time) ? "Build Forge" : $"Publicado: {version.Time}",
                IsRecommended = version.IsRecommendedVersion,
                IsLatest = version.IsLatestVersion
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ModLoaderBuildEntry>> GetOptifineVersionsAsync(string minecraftVersion)
    {
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return [];
        }

        var installer = new OptifineInstaller(HttpClient);
        var versions = await installer.GetOptifineVersionsAsync();

        return versions
            .Where(version => string.Equals(version.MinecraftVersion, minecraftVersion.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(version => version.UploadedDate)
            .Select(version => new ModLoaderBuildEntry
            {
                Id = version.Version,
                DisplayName = BuildOptifineDisplayName(version),
                Meta = $"Upload: {version.UploadedDate:dd/MM/yyyy}" +
                       (string.IsNullOrWhiteSpace(version.ForgeVersion) ? string.Empty : $" | Forge compativel: {version.ForgeVersion}"),
                ForgeCompatibilityVersion = version.ForgeVersion ?? string.Empty,
                IsPreview = version.IsPreviewVersion
            })
            .ToList();
    }

    private static string BuildForgeDisplayName(ForgeVersion version)
    {
        if (version.IsRecommendedVersion)
        {
            return $"{version.ForgeVersionName} (recomendado)";
        }

        if (version.IsLatestVersion)
        {
            return $"{version.ForgeVersionName} (latest)";
        }

        return version.ForgeVersionName;
    }

    private static string BuildOptifineDisplayName(OptifineVersion version)
    {
        var label = version.OptifineEdition.Replace('_', ' ');
        return version.IsPreviewVersion ? $"{label} (preview)" : label;
    }
}
