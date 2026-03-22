using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Bn_s_Launcher.Models;

public sealed class LauncherSettings
{
    public static string InstancesRootDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bn Project", "Instances");

    public static string DefaultGameDirectory => BuildGameDirectory("Instancia principal");

    public List<LauncherProfile> Profiles { get; set; } = [];

    public List<LauncherInstance> Instances { get; set; } = [];

    public string SelectedProfileId { get; set; } = string.Empty;

    public string SelectedInstanceId { get; set; } = string.Empty;

    public DateTime? LastLaunchAt { get; set; }

    public string LastStatus { get; set; } = "Pronto para configurar.";

    public void EnsureDefaults()
    {
        if (Profiles.Count == 0)
        {
            Profiles.Add(new LauncherProfile());
        }

        if (Instances.Count == 0)
        {
            Instances.Add(new LauncherInstance());
        }

        foreach (var profile in Profiles.Where(profile => string.IsNullOrWhiteSpace(profile.Id)))
        {
            profile.Id = Guid.NewGuid().ToString("N");
        }

        foreach (var instance in Instances.Where(instance => string.IsNullOrWhiteSpace(instance.Id)))
        {
            instance.Id = Guid.NewGuid().ToString("N");
        }

        if (Profiles.All(profile => profile.Id != SelectedProfileId))
        {
            SelectedProfileId = Profiles[0].Id;
        }

        if (Instances.All(instance => instance.Id != SelectedInstanceId))
        {
            SelectedInstanceId = Instances[0].Id;
        }

        if (string.IsNullOrWhiteSpace(LastStatus))
        {
            LastStatus = "Pronto para configurar.";
        }
    }

    public LauncherProfile GetSelectedProfile()
    {
        EnsureDefaults();
        return Profiles.First(profile => profile.Id == SelectedProfileId);
    }

    public LauncherInstance GetSelectedInstance()
    {
        EnsureDefaults();
        return Instances.First(instance => instance.Id == SelectedInstanceId);
    }

    public static string BuildGameDirectory(string instanceName)
    {
        var safeName = SanitizeFolderName(instanceName);
        return Path.Combine(InstancesRootDirectory, safeName);
    }

    private static string SanitizeFolderName(string value)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? "Instancia principal" : value.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(raw.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Instancia principal" : cleaned;
    }
}
