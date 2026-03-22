using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bn_s_Launcher.Models;

namespace Bn_s_Launcher.Services;

public sealed class NewsService
{
    private const string ManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";
    private const string DownloadUrl = "https://www.minecraft.net/en-us/download";
    private const string ArticlesUrl = "https://www.minecraft.net/en-us/articles";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public async Task<(IReadOnlyList<LauncherNewsItem> Items, bool UsedFallback)> GetNewsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await HttpClient.GetAsync(ManifestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            return (BuildItemsFromManifest(document.RootElement), false);
        }
        catch
        {
            return (BuildFallbackItems(), true);
        }
    }

    private static IReadOnlyList<LauncherNewsItem> BuildItemsFromManifest(JsonElement root)
    {
        var items = new List<LauncherNewsItem>();
        var latest = root.GetProperty("latest");
        var versions = root.GetProperty("versions").EnumerateArray().ToList();

        var latestReleaseId = latest.GetProperty("release").GetString() ?? "desconhecida";
        var latestSnapshotId = latest.GetProperty("snapshot").GetString() ?? "desconhecida";

        var releaseEntry = versions.FirstOrDefault(version =>
            version.GetProperty("id").GetString() == latestReleaseId);
        var snapshotEntry = versions.FirstOrDefault(version =>
            version.GetProperty("id").GetString() == latestSnapshotId);

        items.Add(new LauncherNewsItem
        {
            Title = $"Release estavel: {latestReleaseId}",
            Summary = "Versao estavel mais recente encontrada no feed oficial da Mojang.",
            Meta = BuildMetaLabel(releaseEntry, "Release oficial"),
            Url = DownloadUrl
        });

        items.Add(new LauncherNewsItem
        {
            Title = $"Snapshot atual: {latestSnapshotId}",
            Summary = "Build de testes mais recente disponivel no manifest publico da Mojang.",
            Meta = BuildMetaLabel(snapshotEntry, "Snapshot oficial"),
            Url = ArticlesUrl
        });

        foreach (var version in versions.Take(3))
        {
            var versionId = version.GetProperty("id").GetString() ?? "versao";
            var versionType = version.GetProperty("type").GetString() ?? "build";

            items.Add(new LauncherNewsItem
            {
                Title = $"Feed recente: {versionId}",
                Summary = $"Entrada recente do canal {versionType} detectada pelo launcher.",
                Meta = BuildMetaLabel(version, versionType),
                Url = versionType.Equals("release", StringComparison.OrdinalIgnoreCase)
                    ? DownloadUrl
                    : ArticlesUrl,
                ActionLabel = "Ver site"
            });
        }

        return items;
    }

    private static List<LauncherNewsItem> BuildFallbackItems()
    {
        return
        [
            new LauncherNewsItem
            {
                Title = "Painel offline pronto",
                Summary = "Nao foi possivel consultar a feed online agora, mas o launcher continua pronto para uso.",
                Meta = "Modo local",
                Url = DownloadUrl
            },
            new LauncherNewsItem
            {
                Title = "Perfis separados",
                Summary = "Use perfis diferentes para PvP, survival, modpack ou contas diferentes sem reconfigurar tudo.",
                Meta = "Dica do launcher",
                Url = ArticlesUrl
            },
            new LauncherNewsItem
            {
                Title = "Instancias dedicadas",
                Summary = "Cada instancia pode apontar para outra pasta, outro Java e outro alvo de launch.",
                Meta = "Dica do launcher",
                Url = ArticlesUrl
            }
        ];
    }

    private static string BuildMetaLabel(JsonElement versionElement, string prefix)
    {
        if (versionElement.ValueKind == JsonValueKind.Undefined)
        {
            return prefix;
        }

        if (versionElement.TryGetProperty("releaseTime", out var releaseTimeProperty) &&
            DateTimeOffset.TryParse(
                releaseTimeProperty.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var releaseTime))
        {
            return $"{prefix} | {releaseTime.ToLocalTime():dd/MM/yyyy HH:mm}";
        }

        return prefix;
    }
}
