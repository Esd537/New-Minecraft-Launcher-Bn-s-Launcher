using System;
using System.IO;
namespace Bn_s_Launcher;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrashLog(args.Exception);
            System.Windows.MessageBox.Show(
                $"Bn`s erro aconteceu e salvou o log em:{Environment.NewLine}{GetCrashLogPath()}",
                "Bn's Launcher",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(-1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                WriteCrashLog(exception);
            }
        };

        base.OnStartup(e);
    }

    private static void WriteCrashLog(Exception exception)
    {
        var logPath = GetCrashLogPath();
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.WriteAllText(logPath, $"{DateTime.Now:O}{Environment.NewLine}{exception}");
    }

    private static string GetCrashLogPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BnsLauncher",
            "launcher-crash.log");
    }
}
