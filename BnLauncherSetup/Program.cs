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
    private const string AppVersion = "1.2.0";
    private const string AppFolderName = "BnsLauncher";
    private const string PayloadResourceName = "BnLauncherSetup.Payload.zip";
    private const string BrandingResourceName = "BnLauncherSetup.Branding.BnLogo.png";
    private const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BnsLauncher";

    private static string DefaultInstallDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            AppFolderName);

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

    private static InstallerResult Install(IProgress<InstallerProgress> progress, InstallOptions options)
    {
        var destinationDirectory = Path.GetFullPath(options.InstallDirectory.Trim());
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new InvalidOperationException("Escolha uma pasta valida para instalar o launcher.");
        }

        progress.Report(new InstallerProgress("Preparando pasta de instalacao...", 12));
        Directory.CreateDirectory(destinationDirectory);

        progress.Report(new InstallerProgress("Extraindo arquivos do launcher...", 42));
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

        progress.Report(new InstallerProgress("Configurando atalhos e desinstalador...", 72));
        var uninstallPath = WriteUninstallScript(destinationDirectory);
        CreateShortcuts(executablePath, destinationDirectory, uninstallPath, options);

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

    private static void CreateShortcuts(string executablePath, string workingDirectory, string uninstallPath, InstallOptions options)
    {
        var desktopShortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"{AppName}.lnk");

        var startMenuFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            AppName);

        var startMenuShortcutPath = Path.Combine(startMenuFolder, $"{AppName}.lnk");
        var uninstallShortcutPath = Path.Combine(startMenuFolder, "Desinstalar Bn's Launcher.lnk");

        if (options.CreateDesktopShortcut)
        {
            CreateShortcut(desktopShortcutPath, executablePath, workingDirectory, executablePath);
        }
        else
        {
            DeleteFileIfExists(desktopShortcutPath);
        }

        if (options.CreateStartMenuShortcut)
        {
            Directory.CreateDirectory(startMenuFolder);
            CreateShortcut(startMenuShortcutPath, executablePath, workingDirectory, executablePath);
            CreateShortcut(uninstallShortcutPath, uninstallPath, Path.GetDirectoryName(uninstallPath)!, executablePath);
        }
        else
        {
            TryDeleteDirectory(startMenuFolder);
        }
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
        var escapedDirectory = destinationDirectory.Replace("\"", "\"\"");
        var uninstallScript = $$"""
@echo off
setlocal
set "TARGET={{escapedDirectory}}"
taskkill /IM "Bn's Launcher.exe" /F >nul 2>nul
rmdir /S /Q "%TARGET%"
rmdir /S /Q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Bn's Launcher" >nul 2>nul
del "%USERPROFILE%\Desktop\Bn's Launcher.lnk" >nul 2>nul
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\BnsLauncher" /f >nul 2>nul
echo Bn's Launcher removido.
pause
""";

        File.WriteAllText(uninstallPath, uninstallScript);
        return uninstallPath;
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed record InstallerProgress(string Message, int Percent);

    private sealed record InstallerResult(string InstallDirectory, string ExecutablePath, string UninstallPath);

    private sealed record InstallOptions(
        string InstallDirectory,
        bool CreateDesktopShortcut,
        bool CreateStartMenuShortcut,
        bool LaunchAfterInstall);

    private sealed class InstallerForm : Form
    {
        private readonly Label _statusLabel;
        private readonly ProgressBar _progressBar;
        private readonly Button _installButton;
        private readonly Button _launchButton;
        private readonly TextBox _installPathTextBox;
        private readonly CheckBox _desktopShortcutCheckBox;
        private readonly CheckBox _startMenuShortcutCheckBox;
        private readonly CheckBox _launchAfterInstallCheckBox;
        private InstallerResult? _result;

        public InstallerForm()
        {
            SuspendLayout();
            Text = $"{AppName} Setup";
            BackColor = Color.FromArgb(9, 11, 17);
            ForeColor = Color.FromArgb(245, 245, 245);
            ClientSize = new Size(860, 520);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            var leftPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 280,
                BackColor = Color.Black
            };

            var logoPicture = new PictureBox
            {
                Image = LoadBrandImage(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Dock = DockStyle.Fill,
                Padding = new Padding(42)
            };
            leftPanel.Controls.Add(logoPicture);

            var titleLabel = new Label
            {
                Text = "Instalador oficial",
                Font = new Font("Bahnschrift", 28, FontStyle.Bold),
                ForeColor = Color.FromArgb(248, 248, 248),
                AutoSize = true,
                Location = new Point(310, 32)
            };

            var subtitleLabel = new Label
            {
                Text = "Escolha a pasta, ative atalhos e instale o launcher com Forge, OptiFine, conta Microsoft e temas customizaveis.",
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                ForeColor = Color.FromArgb(188, 197, 214),
                AutoSize = false,
                Size = new Size(510, 56),
                Location = new Point(310, 80)
            };

            var pathTitleLabel = new Label
            {
                Text = "Pasta de instalacao",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(108, 164, 255),
                AutoSize = true,
                Location = new Point(310, 150)
            };

            _installPathTextBox = new TextBox
            {
                Text = DefaultInstallDirectory,
                Font = new Font("Consolas", 10, FontStyle.Regular),
                BackColor = Color.FromArgb(17, 22, 31),
                ForeColor = Color.FromArgb(244, 244, 244),
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(310, 178),
                Size = new Size(392, 28)
            };

            var browseButton = new Button
            {
                Text = "Buscar pasta",
                BackColor = Color.FromArgb(20, 24, 34),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(714, 176),
                Size = new Size(108, 32)
            };
            browseButton.FlatAppearance.BorderColor = Color.FromArgb(70, 110, 255);
            browseButton.Click += BrowseButton_Click;

            var optionsCard = new Panel
            {
                BackColor = Color.FromArgb(14, 18, 26),
                Location = new Point(310, 226),
                Size = new Size(512, 150)
            };

            var optionsTitleLabel = new Label
            {
                Text = "Opcoes de instalacao",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(248, 248, 248),
                AutoSize = true,
                Location = new Point(18, 16)
            };

            var optionsHintLabel = new Label
            {
                Text = "Marque o que voce quer criar junto com o launcher.",
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(176, 184, 196),
                AutoSize = true,
                Location = new Point(18, 40)
            };

            _desktopShortcutCheckBox = new CheckBox
            {
                Text = "Criar atalho na Area de Trabalho",
                Checked = true,
                AutoSize = true,
                ForeColor = Color.FromArgb(238, 238, 238),
                Location = new Point(22, 74)
            };

            _startMenuShortcutCheckBox = new CheckBox
            {
                Text = "Criar atalho no Menu Iniciar",
                Checked = true,
                AutoSize = true,
                ForeColor = Color.FromArgb(238, 238, 238),
                Location = new Point(22, 102)
            };

            _launchAfterInstallCheckBox = new CheckBox
            {
                Text = "Abrir o launcher automaticamente ao terminar",
                Checked = true,
                AutoSize = true,
                ForeColor = Color.FromArgb(238, 238, 238),
                Location = new Point(22, 130)
            };

            optionsCard.Controls.Add(optionsTitleLabel);
            optionsCard.Controls.Add(optionsHintLabel);
            optionsCard.Controls.Add(_desktopShortcutCheckBox);
            optionsCard.Controls.Add(_startMenuShortcutCheckBox);
            optionsCard.Controls.Add(_launchAfterInstallCheckBox);

            var notesLabel = new Label
            {
                Text = "O instalador registra o app no Windows, cria desinstalador e permite atualizar por cima sem perder a pasta escolhida.",
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 180, 180),
                AutoSize = false,
                Size = new Size(512, 46),
                Location = new Point(310, 390)
            };

            _statusLabel = new Label
            {
                Text = "Pronto para instalar.",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(248, 248, 248),
                AutoSize = true,
                Location = new Point(310, 442)
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(310, 470),
                Size = new Size(512, 14),
                Style = ProgressBarStyle.Continuous,
                Value = 0
            };

            _installButton = new Button
            {
                Text = "Instalar agora",
                BackColor = Color.FromArgb(15, 107, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(310, 492),
                Size = new Size(170, 34)
            };
            _installButton.FlatAppearance.BorderSize = 0;
            _installButton.Click += InstallButton_Click;

            _launchButton = new Button
            {
                Text = "Abrir launcher",
                BackColor = Color.FromArgb(20, 20, 24),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(492, 492),
                Size = new Size(150, 34),
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
                Location = new Point(654, 492),
                Size = new Size(168, 34)
            };
            closeButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
            closeButton.Click += (_, _) => Close();

            Controls.Add(leftPanel);
            Controls.Add(titleLabel);
            Controls.Add(subtitleLabel);
            Controls.Add(pathTitleLabel);
            Controls.Add(_installPathTextBox);
            Controls.Add(browseButton);
            Controls.Add(optionsCard);
            Controls.Add(notesLabel);
            Controls.Add(_statusLabel);
            Controls.Add(_progressBar);
            Controls.Add(_installButton);
            Controls.Add(_launchButton);
            Controls.Add(closeButton);

            ResumeLayout(false);
        }

        private void BrowseButton_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Escolha a pasta onde o launcher sera instalado.",
                SelectedPath = _installPathTextBox.Text.Trim()
            };

            if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                _installPathTextBox.Text = dialog.SelectedPath;
            }
        }

        private async void InstallButton_Click(object? sender, EventArgs e)
        {
            _installButton.Enabled = false;
            _launchButton.Enabled = false;

            try
            {
                var options = BuildInstallOptions();
                var progress = new Progress<InstallerProgress>(value =>
                {
                    _statusLabel.Text = value.Message;
                    _progressBar.Value = Math.Max(0, Math.Min(100, value.Percent));
                });

                _result = await Task.Run(() => Install(progress, options));
                _statusLabel.Text = "Instalado com sucesso. O app ja aparece no Windows.";
                _progressBar.Value = 100;
                _launchButton.Enabled = true;

                if (options.LaunchAfterInstall)
                {
                    LaunchInstalledApp();
                }

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

        private InstallOptions BuildInstallOptions()
        {
            var installDirectory = string.IsNullOrWhiteSpace(_installPathTextBox.Text)
                ? DefaultInstallDirectory
                : _installPathTextBox.Text.Trim();

            return new InstallOptions(
                installDirectory,
                _desktopShortcutCheckBox.Checked,
                _startMenuShortcutCheckBox.Checked,
                _launchAfterInstallCheckBox.Checked);
        }

        private void LaunchButton_Click(object? sender, EventArgs e)
        {
            LaunchInstalledApp();
        }

        private void LaunchInstalledApp()
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
