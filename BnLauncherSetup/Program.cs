using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BnLauncherSetup;

internal static class Program
{
    private const string AppName = "Bn's Launcher";
    private const string AppPublisher = "BN Project";
    private const string AppVersion = "1.1.1";
    private const string AppFolderName = "BnsLauncher";
    private const string PayloadResourceName = "BnLauncherSetup.Payload.zip";
    private const string BrandingResourceName = "BnLauncherSetup.Branding.BnLogo.png";
    private const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BnsLauncher";

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new InstallerForm());
    }

    private static Image LoadBrandImage()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(BrandingResourceName)
            ?? throw new InvalidOperationException("Nao foi possivel carregar a imagem da instalacao.");
        return Image.FromStream(stream);
    }

    private static InstallerResult Install(IProgress<InstallerProgress> progress)
    {
        progress.Report(new InstallerProgress("Preparando pasta de instalacao...", 15));

        var destinationDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            AppFolderName);

        Directory.CreateDirectory(destinationDirectory);

        progress.Report(new InstallerProgress("Extraindo arquivos do launcher...", 45));

        using (var payloadStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResourceName)
               ?? throw new InvalidOperationException("Nao foi possivel localizar os arquivos internos do instalador."))
        {
            ZipFile.ExtractToDirectory(payloadStream, destinationDirectory, overwriteFiles: true);
        }

        var executablePath = Path.Combine(destinationDirectory, "Bn's Launcher.exe");
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("O executavel principal nao foi encontrado apos a instalacao.", executablePath);
        }

        progress.Report(new InstallerProgress("Criando atalhos...", 70));
        var uninstallPath = WriteUninstallScript(destinationDirectory);
        CreateShortcuts(executablePath, destinationDirectory, uninstallPath);

        progress.Report(new InstallerProgress("Registrando o app no Windows...", 88));
        RegisterWindowsApp(destinationDirectory, executablePath, uninstallPath);

        progress.Report(new InstallerProgress("Instalacao concluida.", 100));
        return new InstallerResult(destinationDirectory, executablePath, uninstallPath);
    }

    private static void RegisterWindowsApp(string destinationDirectory, string executablePath, string uninstallPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryPath)
            ?? throw new InvalidOperationException("Nao foi possivel registrar o launcher no Windows.");

        var estimatedSizeKb = Math.Max(1, Directory.GetFiles(destinationDirectory, "*", SearchOption.AllDirectories)
            .Sum(path => new FileInfo(path).Length) / 1024);

        key.SetValue("DisplayName", AppName);
        key.SetValue("DisplayVersion", AppVersion);
        key.SetValue("Publisher", AppPublisher);
        key.SetValue("DisplayIcon", executablePath);
        key.SetValue("InstallLocation", destinationDirectory);
        key.SetValue("UninstallString", uninstallPath);
        key.SetValue("QuietUninstallString", uninstallPath);
        key.SetValue("EstimatedSize", (int)estimatedSizeKb, RegistryValueKind.DWord);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void CreateShortcuts(string executablePath, string workingDirectory, string uninstallPath)
    {
        var desktopShortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"{AppName}.lnk");

        var startMenuFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            AppName);
        Directory.CreateDirectory(startMenuFolder);

        var startMenuShortcutPath = Path.Combine(startMenuFolder, $"{AppName}.lnk");
        var uninstallShortcutPath = Path.Combine(startMenuFolder, "Desinstalar Bn's Launcher.lnk");

        CreateShortcut(desktopShortcutPath, executablePath, workingDirectory, executablePath);
        CreateShortcut(startMenuShortcutPath, executablePath, workingDirectory, executablePath);
        CreateShortcut(uninstallShortcutPath, uninstallPath, Path.GetDirectoryName(uninstallPath)!, executablePath);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string iconPath)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Nao foi possivel acessar o criador de atalhos do Windows.");

        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Nao foi possivel iniciar o criador de atalhos do Windows.");

        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.IconLocation = iconPath;
        shortcut.Save();
    }

    private static string WriteUninstallScript(string destinationDirectory)
    {
        var uninstallPath = Path.Combine(destinationDirectory, "Uninstall Bn's Launcher.cmd");
        var uninstallScript = """
@echo off
setlocal
taskkill /IM "Bn's Launcher.exe" /F >nul 2>nul
rmdir /S /Q "%LOCALAPPDATA%\Programs\BnsLauncher"
rmdir /S /Q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Bn's Launcher" >nul 2>nul
del "%USERPROFILE%\Desktop\Bn's Launcher.lnk" >nul 2>nul
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\BnsLauncher" /f >nul 2>nul
echo Bn's Launcher removido.
pause
""";

        File.WriteAllText(uninstallPath, uninstallScript);
        return uninstallPath;
    }

    private sealed record InstallerProgress(string Message, int Percent);

    private sealed record InstallerResult(string InstallDirectory, string ExecutablePath, string UninstallPath);

    private sealed class InstallerForm : Form
    {
        private readonly Label _statusLabel;
        private readonly ProgressBar _progressBar;
        private readonly Button _installButton;
        private readonly Button _launchButton;
        private readonly Label _pathValueLabel;
        private InstallerResult? _result;

        public InstallerForm()
        {
            SuspendLayout();
            Text = $"{AppName} Setup";
            BackColor = Color.FromArgb(9, 9, 11);
            ForeColor = Color.FromArgb(245, 245, 245);
            ClientSize = new Size(760, 420);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            var leftPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 250,
                BackColor = Color.Black
            };

            var logoPicture = new PictureBox
            {
                Image = LoadBrandImage(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Dock = DockStyle.Fill,
                Padding = new Padding(36)
            };
            leftPanel.Controls.Add(logoPicture);

            var titleLabel = new Label
            {
                Text = "Instalador oficial",
                Font = new Font("Bahnschrift", 24, FontStyle.Bold),
                ForeColor = Color.FromArgb(248, 248, 248),
                AutoSize = true,
                Location = new Point(280, 36)
            };

            var subtitleLabel = new Label
            {
                Text = "Bn's Launcher com Forge, OptiFine, tema custom e login Microsoft.",
                Font = new Font("Segoe UI", 11, FontStyle.Regular),
                ForeColor = Color.FromArgb(194, 202, 214),
                AutoSize = false,
                Size = new Size(430, 44),
                Location = new Point(280, 82)
            };

            var pathTitleLabel = new Label
            {
                Text = "Pasta de instalacao",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(170, 208, 255),
                AutoSize = true,
                Location = new Point(280, 146)
            };

            _pathValueLabel = new Label
            {
                Text = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    AppFolderName),
                Font = new Font("Consolas", 10, FontStyle.Regular),
                ForeColor = Color.FromArgb(238, 238, 238),
                AutoSize = false,
                Size = new Size(430, 52),
                Location = new Point(280, 174)
            };

            var notesLabel = new Label
            {
                Text = "O instalador cria atalho na Area de Trabalho, registra o app no Windows e deixa o launcher pronto para abrir.",
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                ForeColor = Color.FromArgb(186, 186, 186),
                AutoSize = false,
                Size = new Size(430, 60),
                Location = new Point(280, 236)
            };

            _statusLabel = new Label
            {
                Text = "Pronto para instalar.",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(248, 248, 248),
                AutoSize = true,
                Location = new Point(280, 304)
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(280, 332),
                Size = new Size(430, 14),
                Style = ProgressBarStyle.Continuous,
                Value = 0
            };

            _installButton = new Button
            {
                Text = "Instalar agora",
                BackColor = Color.FromArgb(15, 107, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(280, 364),
                Size = new Size(160, 36)
            };
            _installButton.FlatAppearance.BorderSize = 0;
            _installButton.Click += InstallButton_Click;

            _launchButton = new Button
            {
                Text = "Abrir launcher",
                BackColor = Color.FromArgb(20, 20, 24),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(452, 364),
                Size = new Size(140, 36),
                Enabled = false
            };
            _launchButton.FlatAppearance.BorderColor = Color.FromArgb(70, 110, 255);
            _launchButton.Click += LaunchButton_Click;

            var closeButton = new Button
            {
                Text = "Fechar",
                BackColor = Color.FromArgb(20, 20, 24),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(604, 364),
                Size = new Size(106, 36)
            };
            closeButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
            closeButton.Click += (_, _) => Close();

            Controls.Add(leftPanel);
            Controls.Add(titleLabel);
            Controls.Add(subtitleLabel);
            Controls.Add(pathTitleLabel);
            Controls.Add(_pathValueLabel);
            Controls.Add(notesLabel);
            Controls.Add(_statusLabel);
            Controls.Add(_progressBar);
            Controls.Add(_installButton);
            Controls.Add(_launchButton);
            Controls.Add(closeButton);

            ResumeLayout(false);
        }

        private async void InstallButton_Click(object? sender, EventArgs e)
        {
            _installButton.Enabled = false;
            _launchButton.Enabled = false;

            try
            {
                var progress = new Progress<InstallerProgress>(value =>
                {
                    _statusLabel.Text = value.Message;
                    _progressBar.Value = Math.Max(0, Math.Min(100, value.Percent));
                });

                _result = await Task.Run(() => Install(progress));
                _statusLabel.Text = "Instalado com sucesso. O app ja aparece no Windows.";
                _progressBar.Value = 100;
                _launchButton.Enabled = true;

                MessageBox.Show(
                    $"{AppName} instalado com sucesso em:{Environment.NewLine}{_result.InstallDirectory}",
                    "Instalacao concluida",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Falha ao instalar.";
                MessageBox.Show(
                    $"Falha ao instalar o launcher.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                    "Erro de instalacao",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _installButton.Enabled = true;
            }
        }

        private void LaunchButton_Click(object? sender, EventArgs e)
        {
            if (_result is null)
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = _result.ExecutablePath,
                WorkingDirectory = _result.InstallDirectory,
                UseShellExecute = true
            });
        }
    }
}
