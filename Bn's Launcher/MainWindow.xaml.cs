using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Bn_s_Launcher.Models;
using Bn_s_Launcher.Services;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using Optifine.Installer;
using WinForms = System.Windows.Forms;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WpfPoint = System.Windows.Point;
using WpfTextBox = System.Windows.Controls.TextBox;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace Bn_s_Launcher;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly NewsService _newsService = new();
    private readonly LoaderCatalogService _loaderCatalogService = new();
    private LauncherSettings _settings = new();
    private LauncherProfile? _currentProfile;
    private LauncherInstance? _currentInstance;
    private JELoginHandler? _microsoftLoginHandler;
    private MSession? _microsoftSession;
    private List<ModLoaderBuildEntry> _forgeBuilds = [];
    private List<ModLoaderBuildEntry> _optifineBuilds = [];
    private bool _isHydratingUi;
    private bool _isInitializingWindow;
    private bool _isLoadingNews;
    private bool _isLoadingLoaders;
    private bool _isLaunching;
    private bool _isViewReady;
    private bool _isFullscreen;
    private double _restoreLeft = double.NaN;
    private double _restoreTop = double.NaN;
    private double _restoreWidth = double.NaN;
    private double _restoreHeight = double.NaN;
    private WindowState _restoreWindowState = WindowState.Maximized;
    private const string ManagedOptifineModPrefix = "BnLauncher-OptiFine-";

    public MainWindow()
    {
        _isInitializingWindow = true;

        try
        {
            InitializeComponent();
            LoadSettings();
            _isViewReady = true;
        }
        finally
        {
            _isInitializingWindow = false;
        }

        UpdateStatusText();
        UpdatePreview();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SaveCurrentSettings(false);
        base.OnClosing(e);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateWindowModeBadge();
        UpdateAccountUi();
        await RefreshLoaderCatalogAsync();
        await RefreshNewsAsync();
    }

    private void LoadSettings()
    {
        _settings = _settingsService.Load();
        _settings.EnsureDefaults();
        ProfilesListBox.ItemsSource = _settings.Profiles;
        InstancesListBox.ItemsSource = _settings.Instances;
        FooterPathTextBlock.Text = _settingsService.SettingsPath;
        LoadSelectedProfile(_settings.SelectedProfileId);
        LoadSelectedInstance(_settings.SelectedInstanceId);
        LaunchProgressBar.Value = 0;
    }

    private void LoadSelectedProfile(string? id)
    {
        var profile = _settings.Profiles.FirstOrDefault(item => item.Id == id) ?? _settings.Profiles[0];
        _settings.SelectedProfileId = profile.Id;
        _currentProfile = profile;
        ApplyThemePresetToProfile(profile, overwriteAccentColor: false);

        _isHydratingUi = true;
        try
        {
            ProfilesListBox.SelectedItem = profile;
            ProfilePresetNameTextBox.Text = profile.Name;
            SelectComboValue(AccountModeComboBox, profile.AccountMode);
            SelectComboValue(ThemePresetComboBox, profile.ThemePreset);
            PlayerNameTextBox.Text = profile.PlayerName;
            MemorySlider.Value = profile.MemoryMb;
            JvmArgumentsTextBox.Text = profile.JvmArguments;
            GameArgumentsTextBox.Text = profile.GameArguments;
            FullScreenCheckBox.IsChecked = profile.FullScreen;
            CloseOnLaunchCheckBox.IsChecked = profile.CloseOnLaunch;
            AccentColorTextBox.Text = profile.AccentColor;
            BackgroundImageTextBox.Text = profile.BackgroundImagePath;
        }
        finally
        {
            _isHydratingUi = false;
        }

        RefreshProfileListView();
        EnsureMicrosoftAccountLabel(profile);
        UpdateThemeEditorState();
        UpdateAccountUi();
    }

    private void LoadSelectedInstance(string? id)
    {
        var instance = _settings.Instances.FirstOrDefault(item => item.Id == id) ?? _settings.Instances[0];
        _settings.SelectedInstanceId = instance.Id;
        _currentInstance = instance;

        _isHydratingUi = true;
        try
        {
            InstancesListBox.SelectedItem = instance;
            InstanceNameTextBox.Text = instance.Name;
            InstanceNotesTextBox.Text = instance.Notes;
            VersionIdTextBox.Text = instance.VersionId;
            SelectComboValue(VersionTypeComboBox, instance.VersionType);
            SelectComboValue(LoaderKindComboBox, instance.LoaderKind);
            GameDirectoryTextBox.Text = instance.GameDirectory;
            JavaPathTextBox.Text = instance.JavaPath;
            SelectBuildValue(ForgeVersionComboBox, instance.ForgeVersion);
            SelectBuildValue(OptifineVersionComboBox, instance.OptifineVersion);
        }
        finally
        {
            _isHydratingUi = false;
        }

        RefreshInstanceListView();
        UpdateLoaderUi();
        _ = RefreshLoaderCatalogAsync();
    }

    private void ApplyEditorsToCurrentModels()
    {
        if (_currentProfile is not null)
        {
            _currentProfile.Name = ReadText(ProfilePresetNameTextBox, "Perfil principal");
            _currentProfile.AccountMode = ReadComboText(AccountModeComboBox, LauncherProfile.AccountModeOffline);
            _currentProfile.ThemePreset = NormalizeThemePreset(ReadComboText(ThemePresetComboBox, LauncherProfile.ThemePresetBlue));
            _currentProfile.PlayerName = ReadText(PlayerNameTextBox, "SteveBn");
            _currentProfile.MemoryMb = (int)MemorySlider.Value;
            _currentProfile.JvmArguments = ReadText(JvmArgumentsTextBox, new LauncherProfile().JvmArguments);
            _currentProfile.GameArguments = ReadText(GameArgumentsTextBox);
            _currentProfile.FullScreen = FullScreenCheckBox.IsChecked == true;
            _currentProfile.CloseOnLaunch = CloseOnLaunchCheckBox.IsChecked == true;
            _currentProfile.AccentColor = ReadText(AccentColorTextBox, "#0F6BFF");
            _currentProfile.BackgroundImagePath = ReadText(BackgroundImageTextBox);
            _settings.SelectedProfileId = _currentProfile.Id;
        }

        if (_currentInstance is not null)
        {
            _currentInstance.Name = ReadText(InstanceNameTextBox, "Instancia principal");
            _currentInstance.Notes = ReadText(InstanceNotesTextBox, "Sem observacoes.");
            _currentInstance.VersionId = ReadText(VersionIdTextBox, LauncherInstance.DefaultVersionId);
            _currentInstance.VersionType = ReadComboText(VersionTypeComboBox, "release");
            _currentInstance.LoaderKind = ReadComboText(LoaderKindComboBox, LauncherInstance.LoaderKindVanilla);
            _currentInstance.ForgeVersion = ReadSelectedComboValue(ForgeVersionComboBox);
            _currentInstance.OptifineVersion = ReadSelectedComboValue(OptifineVersionComboBox);
            _currentInstance.GameDirectory = ReadText(GameDirectoryTextBox, LauncherSettings.DefaultGameDirectory);
            _currentInstance.JavaPath = ReadText(JavaPathTextBox);
            _settings.SelectedInstanceId = _currentInstance.Id;
        }

        RefreshProfileListView();
        RefreshInstanceListView();
    }

    private void RefreshProfileListView()
    {
        _isHydratingUi = true;
        try
        {
            ProfilesListBox.Items.Refresh();
            if (_currentProfile is not null)
            {
                ProfilesListBox.SelectedItem = _currentProfile;
            }
        }
        finally
        {
            _isHydratingUi = false;
        }
    }

    private void RefreshInstanceListView()
    {
        _isHydratingUi = true;
        try
        {
            InstancesListBox.Items.Refresh();
            if (_currentInstance is not null)
            {
                InstancesListBox.SelectedItem = _currentInstance;
            }
        }
        finally
        {
            _isHydratingUi = false;
        }
    }

    private static string ReadText(WpfTextBox textBox, string fallback = "")
    {
        var value = textBox.Text?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string ReadComboText(WpfComboBox comboBox, string fallback)
    {
        var value = comboBox.Text?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string ReadSelectedComboValue(WpfComboBox comboBox)
    {
        return comboBox.SelectedValue?.ToString()?.Trim() ?? string.Empty;
    }

    private static void SelectComboValue(WpfComboBox comboBox, string value)
    {
        comboBox.Text = value;

        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                break;
            }
        }
    }

    private static void SelectBuildValue(WpfComboBox comboBox, string value)
    {
        if (comboBox is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            comboBox.SelectedIndex = -1;
            return;
        }

        comboBox.SelectedValue = value;
        if (comboBox.SelectedValue?.ToString() == value)
        {
            return;
        }

        foreach (var item in comboBox.Items.OfType<ModLoaderBuildEntry>())
        {
            if (string.Equals(item.Id, value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }

    private static string NormalizeThemePreset(string value)
    {
        return value.ToLowerInvariant() switch
        {
            LauncherProfile.ThemePresetWhite => LauncherProfile.ThemePresetWhite,
            LauncherProfile.ThemePresetRed => LauncherProfile.ThemePresetRed,
            LauncherProfile.ThemePresetCustom => LauncherProfile.ThemePresetCustom,
            _ => LauncherProfile.ThemePresetBlue
        };
    }

    private static string ResolveAccentColorForTheme(string themePreset)
    {
        return NormalizeThemePreset(themePreset) switch
        {
            LauncherProfile.ThemePresetWhite => "#FFFFFF",
            LauncherProfile.ThemePresetRed => "#FF2B2B",
            LauncherProfile.ThemePresetCustom => "#0F6BFF",
            _ => "#0F6BFF"
        };
    }

    private static string InferThemePresetFromAccent(string rawColor)
    {
        var value = rawColor.Trim();
        if (string.Equals(value, "#FFFFFF", StringComparison.OrdinalIgnoreCase))
        {
            return LauncherProfile.ThemePresetWhite;
        }

        if (string.Equals(value, "#FF2B2B", StringComparison.OrdinalIgnoreCase))
        {
            return LauncherProfile.ThemePresetRed;
        }

        if (string.Equals(value, "#0F6BFF", StringComparison.OrdinalIgnoreCase))
        {
            return LauncherProfile.ThemePresetBlue;
        }

        return LauncherProfile.ThemePresetCustom;
    }

    private void ApplyThemePresetToProfile(LauncherProfile profile, bool overwriteAccentColor)
    {
        profile.ThemePreset = NormalizeThemePreset(profile.ThemePreset);
        if (!overwriteAccentColor && string.Equals(profile.ThemePreset, LauncherProfile.ThemePresetCustom, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(profile.ThemePreset, LauncherProfile.ThemePresetCustom, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var accent = ResolveAccentColorForTheme(profile.ThemePreset);
        profile.AccentColor = accent;

        if (AccentColorTextBox is null)
        {
            return;
        }

        _isHydratingUi = true;
        try
        {
            AccentColorTextBox.Text = accent;
        }
        finally
        {
            _isHydratingUi = false;
        }
    }

    private void SyncThemePresetWithAccentEditor()
    {
        if (_currentProfile is null || ThemePresetComboBox is null || AccentColorTextBox is null)
        {
            return;
        }

        var inferredPreset = InferThemePresetFromAccent(ReadText(AccentColorTextBox, _currentProfile.AccentColor));
        _currentProfile.ThemePreset = inferredPreset;

        _isHydratingUi = true;
        try
        {
            SelectComboValue(ThemePresetComboBox, inferredPreset);
        }
        finally
        {
            _isHydratingUi = false;
        }

        UpdateThemeEditorState();
    }

    private void UpdateThemeEditorState()
    {
        if (!_isViewReady || ThemePresetComboBox is null || AccentColorTextBox is null)
        {
            return;
        }

        var preset = NormalizeThemePreset(ReadComboText(ThemePresetComboBox, LauncherProfile.ThemePresetBlue));
        var isCustom = string.Equals(preset, LauncherProfile.ThemePresetCustom, StringComparison.OrdinalIgnoreCase);

        AccentColorTextBox.IsReadOnly = !isCustom;
        AccentColorTextBox.Opacity = isCustom ? 1 : 0.72;
        AccentColorTextBox.ToolTip = isCustom
            ? "Modo custom: escreva uma cor como #0F6BFF."
            : "Escolha o preset acima para trocar o tema do launcher.";
    }

    private static string BuildUniqueName(string prefix, System.Collections.Generic.IEnumerable<string> existingNames)
    {
        var used = existingNames.Where(name => !string.IsNullOrWhiteSpace(name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var index = 1;
        while (used.Contains($"{prefix} {index}"))
        {
            index++;
        }

        return $"{prefix} {index}";
    }

    private void AnySettingChanged(object sender, RoutedEventArgs e)
    {
        if (_isHydratingUi || _isInitializingWindow)
        {
            return;
        }

        ApplyEditorsToCurrentModels();
        UpdateAccountUi();
        UpdateLoaderUi();
        if (ReferenceEquals(sender, AccentColorTextBox))
        {
            SyncThemePresetWithAccentEditor();
        }

        UpdatePreview();
    }

    private void MemorySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isHydratingUi || _isInitializingWindow)
        {
            return;
        }

        ApplyEditorsToCurrentModels();
        UpdatePreview();
    }

    private void AccountModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isHydratingUi || _isInitializingWindow)
        {
            return;
        }

        ApplyEditorsToCurrentModels();
        UpdateAccountUi();
        UpdatePreview();
    }

    private async void LoaderKindComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isHydratingUi || _isInitializingWindow)
        {
            return;
        }

        ApplyEditorsToCurrentModels();
        UpdateLoaderUi();
        UpdatePreview();
        await RefreshLoaderCatalogAsync();
    }

    private void ThemePresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isHydratingUi || _isInitializingWindow)
        {
            return;
        }

        if (_currentProfile is null)
        {
            return;
        }

        ApplyThemePresetToProfile(_currentProfile, overwriteAccentColor: true);
        UpdateThemeEditorState();
        ApplyEditorsToCurrentModels();
        UpdatePreview();
    }

    private void LoaderBuildComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isHydratingUi || _isInitializingWindow)
        {
            return;
        }

        ApplyEditorsToCurrentModels();
        AutoSelectCompatibleForgeBuild();
        UpdateLoaderUi();
        UpdatePreview();
    }

    private void ProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isHydratingUi || _isInitializingWindow)
        {
            return;
        }

        ApplyEditorsToCurrentModels();
        if (ProfilesListBox.SelectedItem is LauncherProfile profile)
        {
            LoadSelectedProfile(profile.Id);
            UpdateAccountUi();
            UpdatePreview();
        }
    }

    private void InstancesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isHydratingUi || _isInitializingWindow)
        {
            return;
        }

        ApplyEditorsToCurrentModels();
        if (InstancesListBox.SelectedItem is LauncherInstance instance)
        {
            LoadSelectedInstance(instance.Id);
            UpdatePreview();
        }
    }

    private void AddProfileButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyEditorsToCurrentModels();
        var profile = new LauncherProfile { Name = BuildUniqueName("Perfil", _settings.Profiles.Select(item => item.Name)) };
        _settings.Profiles.Add(profile);
        LoadSelectedProfile(profile.Id);
        SetStatus($"Novo perfil criado: {profile.Name}.");
    }

    private void DuplicateProfileButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyEditorsToCurrentModels();
        var profile = (_currentProfile ?? _settings.GetSelectedProfile()).Clone(BuildUniqueName("Perfil", _settings.Profiles.Select(item => item.Name)));
        _settings.Profiles.Add(profile);
        LoadSelectedProfile(profile.Id);
        SetStatus($"Perfil duplicado: {profile.Name}.");
    }

    private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.Profiles.Count <= 1)
        {
            WpfMessageBox.Show("Voce precisa manter pelo menos um perfil.", "Bn's Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var profile = _currentProfile ?? _settings.GetSelectedProfile();
        _settings.Profiles.Remove(profile);
        LoadSelectedProfile(_settings.Profiles[0].Id);
        SetStatus($"Perfil removido: {profile.Name}.");
    }

    private void AddInstanceButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyEditorsToCurrentModels();
        var instanceName = BuildUniqueName("Instancia", _settings.Instances.Select(item => item.Name));
        var instance = new LauncherInstance
        {
            Name = instanceName,
            GameDirectory = LauncherSettings.BuildGameDirectory(instanceName)
        };
        _settings.Instances.Add(instance);
        LoadSelectedInstance(instance.Id);
        SetStatus($"Nova instancia criada: {instance.Name}.");
    }

    private void DuplicateInstanceButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyEditorsToCurrentModels();
        var instance = (_currentInstance ?? _settings.GetSelectedInstance()).Clone(BuildUniqueName("Instancia", _settings.Instances.Select(item => item.Name)));
        _settings.Instances.Add(instance);
        LoadSelectedInstance(instance.Id);
        SetStatus($"Instancia duplicada: {instance.Name}.");
    }

    private void DeleteInstanceButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.Instances.Count <= 1)
        {
            WpfMessageBox.Show("Voce precisa manter pelo menos uma instancia.", "Bn's Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var instance = _currentInstance ?? _settings.GetSelectedInstance();
        _settings.Instances.Remove(instance);
        LoadSelectedInstance(_settings.Instances[0].Id);
        SetStatus($"Instancia removida: {instance.Name}.");
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentSettings(true);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _settings = new LauncherSettings();
        _settings.EnsureDefaults();
        ProfilesListBox.ItemsSource = _settings.Profiles;
        InstancesListBox.ItemsSource = _settings.Instances;
        LoadSelectedProfile(_settings.SelectedProfileId);
        LoadSelectedInstance(_settings.SelectedInstanceId);
        SaveCurrentSettings(false);
        SetStatus("Launcher restaurado para o padrao.");
    }

    private void OpenVersionsButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyEditorsToCurrentModels();
        var dialog = new VersionBrowserWindow(_currentInstance?.VersionId)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            VersionIdTextBox.Text = dialog.SelectedVersionId;
            SelectComboValue(VersionTypeComboBox, dialog.SelectedVersionType);

            if (string.IsNullOrWhiteSpace(InstanceNameTextBox.Text) || InstanceNameTextBox.Text == "Instancia principal")
            {
                InstanceNameTextBox.Text = dialog.SelectedVersionId;
            }

            _ = RefreshLoaderCatalogAsync(true);
        }
    }

    private async void MicrosoftLoginButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            MicrosoftLoginButton.IsEnabled = false;
            SetStatus("Abrindo login Microsoft...");
            var session = await EnsureMicrosoftLoginHandler().AuthenticateInteractively();
            ApplyMicrosoftSession(session);
            SaveCurrentSettings(false);
            SetStatus($"Conta Microsoft conectada: {session.Username}.");
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Nao foi possivel entrar com a conta Microsoft.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "Bn's Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            MicrosoftLoginButton.IsEnabled = true;
            UpdateAccountUi();
            UpdatePreview();
        }
    }

    private async void MicrosoftLogoutButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            MicrosoftLogoutButton.IsEnabled = false;
            if (_microsoftLoginHandler is not null)
            {
                await _microsoftLoginHandler.Signout();
            }
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Nao foi possivel sair da conta Microsoft.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "Bn's Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _microsoftSession = null;
            if (_currentProfile is not null)
            {
                _currentProfile.MicrosoftAccountName = string.Empty;
            }

            SaveCurrentSettings(false);
            SetStatus("Conta Microsoft removida do launcher.");
            UpdateAccountUi();
            UpdatePreview();
        }
    }

    private void JavaBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfOpenFileDialog
        {
            Title = "Escolha o Java",
            Filter = "Java|java.exe;javaw.exe|Executaveis|*.exe|Todos os arquivos|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            JavaPathTextBox.Text = dialog.FileName;
        }
    }

    private void BackgroundBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfOpenFileDialog
        {
            Title = "Escolha uma imagem de fundo",
            Filter = "Imagens|*.png;*.jpg;*.jpeg;*.webp;*.bmp|Todos os arquivos|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            BackgroundImageTextBox.Text = dialog.FileName;
        }
    }

    private void GameDirectoryBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Escolha a pasta da instancia",
            UseDescriptionForTitle = true,
            InitialDirectory = Directory.Exists(GameDirectoryTextBox.Text) ? GameDirectoryTextBox.Text : LauncherSettings.DefaultGameDirectory
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            GameDirectoryTextBox.Text = dialog.SelectedPath;
        }
    }

    private void OpenGameFolderButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyEditorsToCurrentModels();
        var instance = _currentInstance ?? _settings.GetSelectedInstance();
        var folder = string.IsNullOrWhiteSpace(instance.GameDirectory) ? LauncherSettings.DefaultGameDirectory : instance.GameDirectory;
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{folder}\"", UseShellExecute = true });
    }

    private async void RefreshLoadersButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshLoaderCatalogAsync(true);
    }

    private async Task RefreshLoaderCatalogAsync(bool forceSelectionRefresh = false)
    {
        if (_isInitializingWindow || !_isViewReady || _isLoadingLoaders)
        {
            return;
        }

        ApplyEditorsToCurrentModels();
        var instance = _currentInstance ?? _settings.GetSelectedInstance();
        var baseVersion = instance.VersionId?.Trim();

        if (string.IsNullOrWhiteSpace(baseVersion))
        {
            _forgeBuilds = [];
            _optifineBuilds = [];
            BindLoaderSelections(forceSelectionRefresh);
            UpdateLoaderUi();
            return;
        }

        _isLoadingLoaders = true;
        LoaderStatusTextBlock.Text = "Atualizando loaders desta versao...";

        try
        {
            if (instance.UsesForge)
            {
                _forgeBuilds = (await _loaderCatalogService.GetForgeVersionsAsync(baseVersion)).ToList();
            }
            else
            {
                _forgeBuilds = [];
            }

            if (instance.UsesOptifine)
            {
                _optifineBuilds = (await _loaderCatalogService.GetOptifineVersionsAsync(baseVersion)).ToList();
            }
            else
            {
                _optifineBuilds = [];
            }

            BindLoaderSelections(forceSelectionRefresh);
        }
        catch (Exception ex)
        {
            if (!instance.UsesForge)
            {
                _forgeBuilds = [];
            }

            if (!instance.UsesOptifine)
            {
                _optifineBuilds = [];
            }

            BindLoaderSelections(forceSelectionRefresh);
            LoaderStatusTextBlock.Text = $"Nao foi possivel carregar Forge/OptiFine agora: {ex.Message}";
        }
        finally
        {
            _isLoadingLoaders = false;
            UpdateLoaderUi();
        }
    }

    private void BindLoaderSelections(bool forceSelectionRefresh)
    {
        if (!_isViewReady || ForgeVersionComboBox is null || OptifineVersionComboBox is null)
        {
            return;
        }

        var instance = _currentInstance ?? _settings.GetSelectedInstance();

        _isHydratingUi = true;
        try
        {
            ForgeVersionComboBox.ItemsSource = _forgeBuilds;
            OptifineVersionComboBox.ItemsSource = _optifineBuilds;

            var forgeVersion = ResolveBuildSelection(
                _forgeBuilds,
                forceSelectionRefresh ? string.Empty : instance.ForgeVersion,
                preferRecommended: true);
            var optifineVersion = ResolveBuildSelection(
                _optifineBuilds,
                forceSelectionRefresh ? string.Empty : instance.OptifineVersion,
                preferRecommended: false);

            instance.ForgeVersion = forgeVersion;
            instance.OptifineVersion = optifineVersion;

            SelectBuildValue(ForgeVersionComboBox, forgeVersion);
            SelectBuildValue(OptifineVersionComboBox, optifineVersion);
        }
        finally
        {
            _isHydratingUi = false;
        }

        AutoSelectCompatibleForgeBuild();
    }

    private static string ResolveBuildSelection(IReadOnlyList<ModLoaderBuildEntry> builds, string currentId, bool preferRecommended)
    {
        if (!string.IsNullOrWhiteSpace(currentId) &&
            builds.Any(item => string.Equals(item.Id, currentId, StringComparison.OrdinalIgnoreCase)))
        {
            return currentId;
        }

        if (preferRecommended)
        {
            var recommended = builds.FirstOrDefault(item => item.IsRecommended);
            if (recommended is not null)
            {
                return recommended.Id;
            }
        }

        var latest = builds.FirstOrDefault(item => item.IsLatest);
        if (latest is not null)
        {
            return latest.Id;
        }

        return builds.FirstOrDefault()?.Id ?? string.Empty;
    }

    private void AutoSelectCompatibleForgeBuild()
    {
        if (_isHydratingUi || _currentInstance is null || _currentInstance.LoaderKind != LauncherInstance.LoaderKindForgeOptifine)
        {
            return;
        }

        if (OptifineVersionComboBox.SelectedItem is not ModLoaderBuildEntry optifineBuild ||
            string.IsNullOrWhiteSpace(optifineBuild.ForgeCompatibilityVersion))
        {
            return;
        }

        var forgeBuild = _forgeBuilds.FirstOrDefault(item =>
            string.Equals(item.Id, optifineBuild.ForgeCompatibilityVersion, StringComparison.OrdinalIgnoreCase));
        if (forgeBuild is null)
        {
            return;
        }

        _isHydratingUi = true;
        try
        {
            ForgeVersionComboBox.SelectedValue = forgeBuild.Id;
            _currentInstance.ForgeVersion = forgeBuild.Id;
        }
        finally
        {
            _isHydratingUi = false;
        }
    }

    private void UpdateLoaderUi()
    {
        if (_isInitializingWindow || !_isViewReady ||
            LoaderKindComboBox is null ||
            ForgeVersionComboBox is null ||
            OptifineVersionComboBox is null ||
            ForgeVersionMetaTextBlock is null ||
            OptifineVersionMetaTextBlock is null ||
            LoaderStatusTextBlock is null)
        {
            return;
        }

        var instance = _currentInstance ?? _settings.GetSelectedInstance();
        var forgeEnabled = instance.UsesForge;
        var optifineEnabled = instance.UsesOptifine;

        ForgeVersionComboBox.IsEnabled = forgeEnabled && _forgeBuilds.Count > 0;
        OptifineVersionComboBox.IsEnabled = optifineEnabled && _optifineBuilds.Count > 0;
        ForgeVersionComboBox.Opacity = forgeEnabled ? 1 : 0.6;
        OptifineVersionComboBox.Opacity = optifineEnabled ? 1 : 0.6;

        ForgeVersionMetaTextBlock.Text = forgeEnabled
            ? ForgeVersionComboBox.SelectedItem is ModLoaderBuildEntry forgeBuild
                ? forgeBuild.Meta
                : _forgeBuilds.Count == 0
                    ? "Nenhuma build Forge listada para esta versao."
                    : "Selecione uma build Forge."
            : "Forge desativado nesta instancia.";

        OptifineVersionMetaTextBlock.Text = optifineEnabled
            ? OptifineVersionComboBox.SelectedItem is ModLoaderBuildEntry optifineBuild
                ? optifineBuild.Meta
                : _optifineBuilds.Count == 0
                    ? "Nenhuma build OptiFine listada para esta versao."
                    : "Selecione uma build OptiFine."
            : "OptiFine desativado nesta instancia.";

        if (_isLoadingLoaders)
        {
            LoaderStatusTextBlock.Text = "Atualizando loaders desta versao...";
            return;
        }

        var fastLaunchLabel = CanReusePreparedVersion(instance, BuildLaunchSignature(instance))
            ? "Launch rapido ativo para a combinacao atual."
            : "Depois do primeiro preparo, o launcher reutiliza a instancia para abrir bem mais rapido.";

        LoaderStatusTextBlock.Text = instance.LoaderKind switch
        {
            LauncherInstance.LoaderKindForge => _forgeBuilds.Count == 0
                ? "Clique em Atualizar para carregar as builds Forge desta versao."
                : $"{_forgeBuilds.Count} build(s) Forge disponiveis. {fastLaunchLabel}",
            LauncherInstance.LoaderKindOptifine => _optifineBuilds.Count == 0
                ? "Clique em Atualizar para carregar as builds OptiFine desta versao."
                : $"{_optifineBuilds.Count} build(s) OptiFine disponiveis. {fastLaunchLabel}",
            LauncherInstance.LoaderKindForgeOptifine => _forgeBuilds.Count == 0 || _optifineBuilds.Count == 0
                ? "Atualize as listas para encaixar Forge e OptiFine nesta versao."
                : $"Modo combo pronto. O launcher tenta casar a build Forge compativel com o OptiFine escolhido. {fastLaunchLabel}",
            _ => fastLaunchLabel
        };
    }

    private static string BuildLaunchSignature(LauncherInstance instance)
    {
        return string.Join("|",
            instance.VersionId.Trim(),
            instance.VersionType.Trim(),
            instance.LoaderKind.Trim(),
            instance.ForgeVersion.Trim(),
            instance.OptifineVersion.Trim());
    }

    private static string GetVersionJsonPath(LauncherInstance instance, string versionId)
    {
        return Path.Combine(instance.GameDirectory, "versions", versionId, $"{versionId}.json");
    }

    private bool CanReusePreparedVersion(LauncherInstance instance, string signature)
    {
        if (!string.Equals(signature, instance.PreparedSignature, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(instance.PreparedVersionId) ||
            !File.Exists(GetVersionJsonPath(instance, instance.PreparedVersionId)))
        {
            return false;
        }

        if (instance.LoaderKind == LauncherInstance.LoaderKindForgeOptifine)
        {
            var modsDirectory = Path.Combine(instance.GameDirectory, "mods");
            return Directory.Exists(modsDirectory) &&
                   Directory.EnumerateFiles(modsDirectory, $"{ManagedOptifineModPrefix}*.jar").Any();
        }

        return true;
    }

    private async Task<ModLoaderBuildEntry> EnsureForgeBuildSelectionAsync(LauncherInstance instance, string? preferredForgeVersion = null)
    {
        if (_forgeBuilds.Count == 0)
        {
            _forgeBuilds = (await _loaderCatalogService.GetForgeVersionsAsync(instance.VersionId)).ToList();
            BindLoaderSelections(false);
        }

        var forgeId = !string.IsNullOrWhiteSpace(preferredForgeVersion) &&
                      _forgeBuilds.Any(item => string.Equals(item.Id, preferredForgeVersion, StringComparison.OrdinalIgnoreCase))
            ? preferredForgeVersion
            : ResolveBuildSelection(_forgeBuilds, instance.ForgeVersion, preferRecommended: true);

        var forgeBuild = _forgeBuilds.FirstOrDefault(item => string.Equals(item.Id, forgeId, StringComparison.OrdinalIgnoreCase));
        if (forgeBuild is null)
        {
            throw new InvalidOperationException($"Nao encontrei uma build Forge para {instance.VersionId}.");
        }

        instance.ForgeVersion = forgeBuild.Id;
        SelectBuildValue(ForgeVersionComboBox, forgeBuild.Id);
        UpdateLoaderUi();
        return forgeBuild;
    }

    private async Task<ModLoaderBuildEntry> EnsureOptifineBuildSelectionAsync(LauncherInstance instance)
    {
        if (_optifineBuilds.Count == 0)
        {
            _optifineBuilds = (await _loaderCatalogService.GetOptifineVersionsAsync(instance.VersionId)).ToList();
            BindLoaderSelections(false);
        }

        var optifineId = ResolveBuildSelection(_optifineBuilds, instance.OptifineVersion, preferRecommended: false);
        var optifineBuild = _optifineBuilds.FirstOrDefault(item => string.Equals(item.Id, optifineId, StringComparison.OrdinalIgnoreCase));
        if (optifineBuild is null)
        {
            throw new InvalidOperationException($"Nao encontrei uma build OptiFine para {instance.VersionId}.");
        }

        instance.OptifineVersion = optifineBuild.Id;
        SelectBuildValue(OptifineVersionComboBox, optifineBuild.Id);
        UpdateLoaderUi();
        return optifineBuild;
    }

    private async Task<string> PrepareLaunchVersionAsync(
        MinecraftLauncher launcher,
        LauncherInstance instance,
        IProgress<InstallerProgressChangedEventArgs> fileProgress,
        IProgress<ByteProgress> byteProgress,
        bool forceInstall = false)
    {
        var signature = BuildLaunchSignature(instance);
        if (!forceInstall && CanReusePreparedVersion(instance, signature))
        {
            return instance.PreparedVersionId;
        }

        CleanupManagedOptifineMods(instance.GameDirectory);

        string resolvedVersionId;
        switch (instance.LoaderKind)
        {
            case LauncherInstance.LoaderKindForge:
                resolvedVersionId = await PrepareForgeAsync(launcher, instance, fileProgress, byteProgress);
                break;
            case LauncherInstance.LoaderKindOptifine:
                resolvedVersionId = await PrepareOptifineAsync(launcher, instance, fileProgress, byteProgress);
                break;
            case LauncherInstance.LoaderKindForgeOptifine:
                resolvedVersionId = await PrepareForgeWithOptifineAsync(launcher, instance, fileProgress, byteProgress);
                break;
            default:
                SetStatus($"Preparando cache rapido de {instance.VersionId}...");
                await launcher.InstallAsync(instance.VersionId, fileProgress, byteProgress);
                resolvedVersionId = instance.VersionId;
                break;
        }

        instance.PreparedSignature = signature;
        instance.PreparedVersionId = resolvedVersionId;
        instance.PreparedAt = DateTime.Now;
        return resolvedVersionId;
    }

    private async Task<string> PrepareForgeAsync(
        MinecraftLauncher launcher,
        LauncherInstance instance,
        IProgress<InstallerProgressChangedEventArgs> fileProgress,
        IProgress<ByteProgress> byteProgress)
    {
        var forgeBuild = await EnsureForgeBuildSelectionAsync(instance);
        SetStatus($"Preparando Forge {forgeBuild.Id}...");

        var installer = new ForgeInstaller(launcher);
        var resolvedVersionId = await installer.Install(
            instance.VersionId,
            forgeBuild.Id,
            new ForgeInstallOptions
            {
                JavaPath = string.IsNullOrWhiteSpace(instance.JavaPath) ? null : instance.JavaPath,
                FileProgress = fileProgress,
                ByteProgress = byteProgress,
                InstallerOutput = new Progress<string>(message => Dispatcher.Invoke(() => StatusTextBlock.Text = message))
            });

        await launcher.InstallAsync(resolvedVersionId, fileProgress, byteProgress);
        return resolvedVersionId;
    }

    private async Task<string> PrepareOptifineAsync(
        MinecraftLauncher launcher,
        LauncherInstance instance,
        IProgress<InstallerProgressChangedEventArgs> fileProgress,
        IProgress<ByteProgress> byteProgress)
    {
        var optifineBuild = await EnsureOptifineBuildSelectionAsync(instance);
        SetStatus($"Preparando OptiFine {optifineBuild.DisplayName}...");

        await launcher.InstallAsync(instance.VersionId, fileProgress, byteProgress);

        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        var optifineInstaller = new OptifineInstaller(client);
        var resolvedVersionId = await optifineInstaller.InstallOptifineAsync(instance.GameDirectory, optifineBuild.Id);

        await launcher.InstallAsync(resolvedVersionId, fileProgress, byteProgress);
        return resolvedVersionId;
    }

    private async Task<string> PrepareForgeWithOptifineAsync(
        MinecraftLauncher launcher,
        LauncherInstance instance,
        IProgress<InstallerProgressChangedEventArgs> fileProgress,
        IProgress<ByteProgress> byteProgress)
    {
        var optifineBuild = await EnsureOptifineBuildSelectionAsync(instance);
        var forgeBuild = await EnsureForgeBuildSelectionAsync(instance, optifineBuild.ForgeCompatibilityVersion);
        SetStatus($"Preparando Forge {forgeBuild.Id} + OptiFine {optifineBuild.DisplayName}...");

        await launcher.InstallAsync(instance.VersionId, fileProgress, byteProgress);

        using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(45) })
        {
            var optifineInstaller = new OptifineInstaller(client);
            await optifineInstaller.InstallOptifineAsync(instance.GameDirectory, optifineBuild.Id);
        }

        CopyManagedOptifineMod(instance, optifineBuild.Id);

        var forgeInstaller = new ForgeInstaller(launcher);
        var resolvedVersionId = await forgeInstaller.Install(
            instance.VersionId,
            forgeBuild.Id,
            new ForgeInstallOptions
            {
                JavaPath = string.IsNullOrWhiteSpace(instance.JavaPath) ? null : instance.JavaPath,
                FileProgress = fileProgress,
                ByteProgress = byteProgress,
                InstallerOutput = new Progress<string>(message => Dispatcher.Invoke(() => StatusTextBlock.Text = message))
            });

        await launcher.InstallAsync(resolvedVersionId, fileProgress, byteProgress);
        return resolvedVersionId;
    }

    private static void CleanupManagedOptifineMods(string gameDirectory)
    {
        var modsDirectory = Path.Combine(gameDirectory, "mods");
        if (!Directory.Exists(modsDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(modsDirectory, $"{ManagedOptifineModPrefix}*.jar"))
        {
            File.Delete(file);
        }
    }

    private static void CopyManagedOptifineMod(LauncherInstance instance, string optifineVersionId)
    {
        var edition = ExtractOptifineEdition(instance.VersionId, optifineVersionId);
        var sourcePath = Path.Combine(
            instance.GameDirectory,
            "libraries",
            "optifine",
            "OptiFine",
            $"{instance.VersionId}_{edition}",
            $"OptiFine-{instance.VersionId}_{edition}.jar");

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Nao encontrei o jar do OptiFine depois da instalacao.", sourcePath);
        }

        var modsDirectory = Path.Combine(instance.GameDirectory, "mods");
        Directory.CreateDirectory(modsDirectory);
        CleanupManagedOptifineMods(instance.GameDirectory);

        var destinationPath = Path.Combine(modsDirectory, $"{ManagedOptifineModPrefix}{instance.VersionId}_{edition}.jar");
        File.Copy(sourcePath, destinationPath, true);
    }

    private static string ExtractOptifineEdition(string minecraftVersion, string optifineVersionId)
    {
        var normalized = optifineVersionId.StartsWith("preview_", StringComparison.OrdinalIgnoreCase)
            ? optifineVersionId["preview_".Length..]
            : optifineVersionId;

        var prefix = $"OptiFine_{minecraftVersion}_";
        if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Nao foi possivel interpretar a build do OptiFine: {optifineVersionId}");
        }

        return normalized[prefix.Length..];
    }

    private async void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isLaunching)
        {
            return;
        }

        ApplyEditorsToCurrentModels();
        var profile = _currentProfile ?? _settings.GetSelectedProfile();
        var instance = _currentInstance ?? _settings.GetSelectedInstance();

        if (string.IsNullOrWhiteSpace(instance.VersionId))
        {
            WpfMessageBox.Show("Escolha primeiro uma versao do Minecraft para a instancia ativa.", "Bn's Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _isLaunching = true;
            LaunchButton.IsEnabled = false;
            LaunchProgressBar.IsIndeterminate = true;
            LaunchProgressBar.Value = 0;

            Directory.CreateDirectory(instance.GameDirectory);
            var minecraftPath = new MinecraftPath(instance.GameDirectory);
            var launcher = new MinecraftLauncher(minecraftPath);
            var fileProgress = new Progress<InstallerProgressChangedEventArgs>(args => Dispatcher.Invoke(() =>
            {
                LaunchProgressBar.IsIndeterminate = false;
                if (args.TotalTasks > 0)
                {
                    LaunchProgressBar.Value = (double)args.ProgressedTasks / args.TotalTasks * 100d;
                }

                StatusTextBlock.Text = $"{args.EventType}: {args.Name}";
            }));

            var byteProgress = new Progress<ByteProgress>(args => Dispatcher.Invoke(() =>
            {
                if (args.TotalBytes > 0)
                {
                    LaunchProgressBar.IsIndeterminate = false;
                    LaunchProgressBar.Value = (double)args.ProgressedBytes / args.TotalBytes * 100d;
                }
            }));

            SetStatus($"Preparando {instance.VersionId}...");
            var resolvedVersionId = await PrepareLaunchVersionAsync(launcher, instance, fileProgress, byteProgress);

            var session = await ResolveLaunchSessionAsync(profile);
            Process process;
            try
            {
                process = await launcher.BuildProcessAsync(resolvedVersionId, BuildLaunchOption(profile, instance, session));
            }
            catch when (CanReusePreparedVersion(instance, BuildLaunchSignature(instance)))
            {
                SetStatus("A instancia mudou por fora. Revalidando os arquivos...");
                resolvedVersionId = await PrepareLaunchVersionAsync(
                    launcher,
                    instance,
                    fileProgress,
                    byteProgress,
                    forceInstall: true);
                process = await launcher.BuildProcessAsync(resolvedVersionId, BuildLaunchOption(profile, instance, session));
            }
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => Dispatcher.Invoke(() =>
            {
                _isLaunching = false;
                LaunchButton.IsEnabled = true;
                LaunchProgressBar.IsIndeterminate = false;
                LaunchProgressBar.Value = 0;
                SetStatus($"Minecraft finalizado em {instance.Name}.");
            });

            instance.LastPlayedAt = DateTime.Now;
            _settings.LastLaunchAt = instance.LastPlayedAt;
            process.Start();
            SetStatus($"Minecraft iniciado: {instance.LoaderDisplayName} em {resolvedVersionId}.");
            SaveCurrentSettings(false);

            if (profile.CloseOnLaunch)
            {
                Close();
            }
        }
        catch (Exception ex)
        {
            _isLaunching = false;
            LaunchButton.IsEnabled = true;
            LaunchProgressBar.IsIndeterminate = false;
            LaunchProgressBar.Value = 0;
            SetStatus("Falha ao iniciar o Minecraft.");
            var javaHint = ex.ToString().Contains("java", StringComparison.OrdinalIgnoreCase)
                ? $"{Environment.NewLine}{Environment.NewLine}Dica: escolha um `java.exe` no campo Java da instancia ou instale Java 21."
                : string.Empty;
            var accountHint = profile.AccountMode == LauncherProfile.AccountModeMicrosoft
                ? $"{Environment.NewLine}{Environment.NewLine}Dica: confirme o login da conta Microsoft e tente novamente."
                : string.Empty;
            var loaderHint = instance.LoaderKind != LauncherInstance.LoaderKindVanilla
                ? $"{Environment.NewLine}{Environment.NewLine}Dica: clique em `Atualizar` na area de loaders e confira se a build escolhida existe para {instance.VersionId}."
                : string.Empty;
            WpfMessageBox.Show(
                $"Falha ao iniciar o Minecraft.{Environment.NewLine}{Environment.NewLine}{ex.Message}{javaHint}{accountHint}{loaderHint}",
                "Bn's Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static MLaunchOption BuildLaunchOption(LauncherProfile profile, LauncherInstance instance, MSession session)
    {
        var option = new MLaunchOption
        {
            Session = session,
            MaximumRamMb = profile.MemoryMb,
            MinimumRamMb = Math.Min(1024, profile.MemoryMb),
            FullScreen = profile.FullScreen,
            GameLauncherName = "BnLauncher",
            GameLauncherVersion = "1.1.1"
        };

        if (!string.IsNullOrWhiteSpace(instance.JavaPath))
        {
            option.JavaPath = instance.JavaPath;
        }

        if (!string.IsNullOrWhiteSpace(profile.JvmArguments))
        {
            option.ExtraJvmArguments = [MArgument.FromCommandLine(profile.JvmArguments)];
        }

        var extraGameArguments = ResolveGameArguments(profile);
        if (!string.IsNullOrWhiteSpace(extraGameArguments))
        {
            option.ExtraGameArguments = [MArgument.FromCommandLine(extraGameArguments)];
        }

        return option;
    }

    private static string ResolveGameArguments(LauncherProfile profile)
    {
        var arguments = (profile.GameArguments ?? string.Empty).Replace("{player}", profile.GetDisplayName(), StringComparison.OrdinalIgnoreCase).Trim();
        if (profile.FullScreen && !arguments.Contains("--fullscreen", StringComparison.OrdinalIgnoreCase))
        {
            arguments = $"{arguments} --fullscreen".Trim();
        }

        return arguments;
    }

    private async Task<MSession> ResolveLaunchSessionAsync(LauncherProfile profile)
    {
        if (profile.AccountMode != LauncherProfile.AccountModeMicrosoft)
        {
            return MSession.CreateOfflineSession(profile.PlayerName);
        }

        var handler = EnsureMicrosoftLoginHandler();

        try
        {
            var session = await handler.AuthenticateSilently();
            ApplyMicrosoftSession(session);
            return session;
        }
        catch
        {
            var session = await handler.AuthenticateInteractively();
            ApplyMicrosoftSession(session);
            return session;
        }
    }

    private JELoginHandler EnsureMicrosoftLoginHandler()
    {
        _microsoftLoginHandler ??= JELoginHandlerBuilder.BuildDefault();
        return _microsoftLoginHandler;
    }

    private void ApplyMicrosoftSession(MSession session)
    {
        _microsoftSession = session;
        if (_currentProfile is not null)
        {
            _currentProfile.MicrosoftAccountName = session.Username ?? string.Empty;
        }

        UpdateAccountUi();
        UpdatePreview();
    }

    private void EnsureMicrosoftAccountLabel(LauncherProfile profile)
    {
        if (profile.AccountMode != LauncherProfile.AccountModeMicrosoft || !string.IsNullOrWhiteSpace(profile.MicrosoftAccountName))
        {
            return;
        }

        var storedAccountName = TryGetStoredMicrosoftAccountName();
        if (!string.IsNullOrWhiteSpace(storedAccountName))
        {
            profile.MicrosoftAccountName = storedAccountName;
        }
    }

    private void UpdateAccountUi()
    {
        if (_isInitializingWindow || !_isViewReady ||
            AccountModeComboBox is null ||
            PlayerNameLabelTextBlock is null ||
            PlayerNameTextBox is null ||
            AccountStatusTextBlock is null ||
            MicrosoftLoginButton is null ||
            MicrosoftLogoutButton is null)
        {
            return;
        }

        var profile = _currentProfile ?? _settings.GetSelectedProfile();
        EnsureMicrosoftAccountLabel(profile);

        var isMicrosoft = string.Equals(profile.AccountMode, LauncherProfile.AccountModeMicrosoft, StringComparison.OrdinalIgnoreCase);
        var microsoftName = _microsoftSession?.Username;
        if (string.IsNullOrWhiteSpace(microsoftName))
        {
            microsoftName = profile.MicrosoftAccountName;
        }

        PlayerNameLabelTextBlock.Text = isMicrosoft ? "Nick offline guardado" : "Nick pirata / offline";
        PlayerNameTextBox.IsEnabled = !isMicrosoft;
        PlayerNameTextBox.Opacity = isMicrosoft ? 0.72 : 1;
        PlayerNameTextBox.ToolTip = isMicrosoft ? "Esse nick so vale para o modo offline/pirata." : null;

        MicrosoftLoginButton.Visibility = isMicrosoft ? Visibility.Visible : Visibility.Collapsed;
        MicrosoftLogoutButton.Visibility = isMicrosoft ? Visibility.Visible : Visibility.Collapsed;
        MicrosoftLogoutButton.IsEnabled = isMicrosoft && !string.IsNullOrWhiteSpace(microsoftName);

        AccountStatusTextBlock.Text = isMicrosoft
            ? string.IsNullOrWhiteSpace(microsoftName)
                ? "Nenhuma conta Microsoft conectada ainda."
                : $"Conta original conectada: {microsoftName}"
            : "Modo offline/pirata ativo. O nick acima sera usado no launch offline.";
    }

    private string? TryGetStoredMicrosoftAccountName()
    {
        try
        {
            var account = EnsureMicrosoftLoginHandler().AccountManager.GetDefaultAccount();
            return ReadPropertyString(account, "Username", "Name");
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadPropertyString(object? target, params string[] propertyNames)
    {
        if (target is null)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            var property = target.GetType().GetProperty(propertyName);
            var value = property?.GetValue(target)?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private void SaveCurrentSettings(bool showSavedStatus)
    {
        ApplyEditorsToCurrentModels();
        _settingsService.Save(_settings);
        if (showSavedStatus)
        {
            SetStatus("Configuracao salva com sucesso.");
        }
    }

    private void SetStatus(string message)
    {
        _settings.LastStatus = message;
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        if (_isInitializingWindow || !_isViewReady || StatusTextBlock is null || LastLaunchValueTextBlock is null || FooterPathTextBlock is null)
        {
            return;
        }

        StatusTextBlock.Text = _settings.LastStatus;
        LastLaunchValueTextBlock.Text = _settings.LastLaunchAt.HasValue ? _settings.LastLaunchAt.Value.ToString("dd/MM/yyyy HH:mm") : "Nenhum launch ainda";
        FooterPathTextBlock.Text = _settingsService.SettingsPath;
    }

    private void UpdatePreview()
    {
        if (_isInitializingWindow || !_isViewReady ||
            MemoryValueTextBlock is null ||
            HeaderProfileBadgeTextBlock is null ||
            HeaderInstanceBadgeTextBlock is null ||
            HeaderVersionBadgeTextBlock is null ||
            HeaderMemoryBadgeTextBlock is null ||
            SummaryProfileValueTextBlock is null ||
            SummaryInstanceValueTextBlock is null ||
            SummaryVersionValueTextBlock is null ||
            SummaryMemoryValueTextBlock is null ||
            SummaryTargetValueTextBlock is null ||
            PreviewProfileTextBlock is null ||
            PreviewStatusTextBlock is null ||
            LaunchButton is null ||
            LaunchProgressBar is null ||
            PreviewAccentBar is null ||
            AccentSwatchBorder is null ||
            WindowModeBadgeTextBlock is null ||
            PreviewBackgroundLayer is null)
        {
            return;
        }

        var profile = _currentProfile ?? _settings.GetSelectedProfile();
        var instance = _currentInstance ?? _settings.GetSelectedInstance();
        var accentBrush = CreateAccentBrush(profile.AccentColor);
        var displayName = profile.GetDisplayName();
        var accountLabel = profile.AccountMode == LauncherProfile.AccountModeMicrosoft ? "microsoft" : "offline";
        var versionBadge = instance.LoaderKind == LauncherInstance.LoaderKindVanilla
            ? instance.VersionId
            : $"{instance.LoaderDisplayName} {instance.VersionId}";

        MemoryValueTextBlock.Text = $"{profile.MemoryMb} MB";
        HeaderProfileBadgeTextBlock.Text = profile.Name;
        HeaderInstanceBadgeTextBlock.Text = instance.Name;
        HeaderVersionBadgeTextBlock.Text = versionBadge;
        HeaderMemoryBadgeTextBlock.Text = $"{profile.MemoryMb} MB";
        SummaryProfileValueTextBlock.Text = profile.Name;
        SummaryInstanceValueTextBlock.Text = instance.Name;
        SummaryVersionValueTextBlock.Text = versionBadge;
        SummaryMemoryValueTextBlock.Text = $"{profile.MemoryMb / 1024d:0.0} GB";
        SummaryTargetValueTextBlock.Text = instance.GameDirectory;
        PreviewProfileTextBlock.Text = displayName;
        PreviewStatusTextBlock.Text = $"{instance.LoaderDisplayName} | {instance.VersionId} | {accountLabel} | {profile.MemoryMb} MB";

        ApplyThemePalette(profile, accentBrush);
        LaunchButton.Background = accentBrush;
        LaunchButton.BorderBrush = accentBrush;
        PreviewAccentBar.Background = accentBrush;
        AccentSwatchBorder.Background = accentBrush;
        AccentSwatchBorder.BorderBrush = accentBrush;
        LaunchProgressBar.Foreground = accentBrush;
        ApplyPreviewBackground(profile.BackgroundImagePath, accentBrush);
        UpdateThemeEditorState();
        UpdateWindowModeBadge();
        UpdateStatusText();
    }

    private void ApplyPreviewBackground(string imagePath, SolidColorBrush accentBrush)
    {
        if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            PreviewBackgroundLayer.Background = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill, Opacity = 0.68 };
            return;
        }

        var color = accentBrush.Color;
        PreviewBackgroundLayer.Background = new LinearGradientBrush(
            MediaColor.FromArgb(255, (byte)Math.Min(255, color.R + 18), (byte)Math.Min(255, color.G + 14), (byte)Math.Min(255, color.B + 14)),
            MediaColor.FromArgb(255, 7, 18, 14),
            new WpfPoint(0, 0),
            new WpfPoint(1, 1));
    }

    private static SolidColorBrush CreateAccentBrush(string rawColor)
    {
        try
        {
            return new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString(rawColor)!);
        }
        catch
        {
            return new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#0F6BFF")!);
        }
    }

    private void ApplyThemePalette(LauncherProfile profile, SolidColorBrush accentBrush)
    {
        var accentColor = accentBrush.Color;
        var preset = NormalizeThemePreset(profile.ThemePreset);
        var accentSoft = WithAlpha(BlendColor(accentColor, MediaColor.FromRgb(255, 255, 255), 0.78), 0x66);
        var labelColor = preset switch
        {
            LauncherProfile.ThemePresetWhite => MediaColor.FromRgb(250, 250, 250),
            LauncherProfile.ThemePresetRed => MediaColor.FromRgb(255, 176, 176),
            _ => MediaColor.FromRgb(170, 208, 255)
        };
        var glowSecondary = preset switch
        {
            LauncherProfile.ThemePresetWhite => MediaColor.FromArgb(0x28, 255, 255, 255),
            LauncherProfile.ThemePresetRed => MediaColor.FromArgb(0x2A, 255, 54, 54),
            _ => MediaColor.FromArgb(0x2A, 15, 107, 255)
        };
        var glowPrimary = WithAlpha(accentColor, 0x26);
        var glowTertiary = WithAlpha(BlendColor(accentColor, MediaColor.FromRgb(255, 255, 255), 0.34), 0x20);

        SetBrushColor("ThemeAccentBrush", accentColor);
        SetBrushColor("ThemeAccentSoftBrush", accentSoft);
        SetBrushColor("ThemeLabelTextBrush", labelColor);
        SetBrushColor("ThemeButtonBorderBrush", WithAlpha(accentColor, 0x72));
        SetBrushColor("ThemeCardBorderBrush", WithAlpha(accentColor, 0x3C));

        WindowBackgroundStartStop.Color = MediaColor.FromRgb(4, 7, 11);
        WindowBackgroundMiddleStop.Color = BlendColor(MediaColor.FromRgb(12, 17, 24), accentColor, 0.22);
        WindowBackgroundEndStop.Color = MediaColor.FromRgb(1, 3, 5);

        GlowPrimaryEllipse.Fill = new SolidColorBrush(glowPrimary);
        GlowSecondaryEllipse.Fill = new SolidColorBrush(glowSecondary);
        GlowTertiaryEllipse.Fill = new SolidColorBrush(glowTertiary);
        ShellBorder.BorderBrush = new SolidColorBrush(WithAlpha(accentColor, 0x20));
        TitleBarBorder.BorderBrush = new SolidColorBrush(WithAlpha(accentColor, 0x28));
    }

    private void SetBrushColor(string resourceKey, MediaColor color)
    {
        if (Resources[resourceKey] is SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }

    private static MediaColor BlendColor(MediaColor baseColor, MediaColor mixColor, double ratio)
    {
        ratio = Math.Max(0, Math.Min(1, ratio));
        var inverse = 1 - ratio;
        return MediaColor.FromRgb(
            (byte)Math.Round(baseColor.R * inverse + mixColor.R * ratio),
            (byte)Math.Round(baseColor.G * inverse + mixColor.G * ratio),
            (byte)Math.Round(baseColor.B * inverse + mixColor.B * ratio));
    }

    private static MediaColor WithAlpha(MediaColor color, byte alpha)
    {
        return MediaColor.FromArgb(alpha, color.R, color.G, color.B);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            e.Handled = true;
            ToggleFullscreen();
        }
    }

    private void FullscreenButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isFullscreen)
        {
            ToggleFullscreen();
            return;
        }

        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateWindowModeBadge();
    }

    private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBarBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeWindowButton_Click(sender, new RoutedEventArgs());
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _restoreWindowState = WindowState;
            _restoreLeft = Left;
            _restoreTop = Top;
            _restoreWidth = Width;
            _restoreHeight = Height;

            var screen = WinForms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            Left = screen.Bounds.Left;
            Top = screen.Bounds.Top;
            Width = screen.Bounds.Width;
            Height = screen.Bounds.Height;
            _isFullscreen = true;
        }
        else
        {
            Topmost = false;
            ResizeMode = ResizeMode.CanResize;
            Left = double.IsNaN(_restoreLeft) ? Left : _restoreLeft;
            Top = double.IsNaN(_restoreTop) ? Top : _restoreTop;
            Width = double.IsNaN(_restoreWidth) ? Width : _restoreWidth;
            Height = double.IsNaN(_restoreHeight) ? Height : _restoreHeight;
            WindowState = _restoreWindowState;
            _isFullscreen = false;
        }

        UpdateWindowModeBadge();
    }

    private void UpdateWindowModeBadge()
    {
        if (!_isViewReady || WindowModeBadgeTextBlock is null || MaximizeWindowButton is null)
        {
            return;
        }

        WindowModeBadgeTextBlock.Text = _isFullscreen
            ? "F11 para sair da tela cheia"
            : WindowState == WindowState.Maximized
                ? "Janela clean maximizada"
                : "F11 tela cheia";
        MaximizeWindowButton.Content = WindowState == WindowState.Maximized ? "[ ]" : "[]";
    }

    private async void RefreshNewsButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshNewsAsync();
    }

    private async Task RefreshNewsAsync()
    {
        if (_isLoadingNews)
        {
            return;
        }

        _isLoadingNews = true;
        RefreshNewsButton.IsEnabled = false;
        NewsStatusTextBlock.Text = "Atualizando painel...";
        try
        {
            var (items, usedFallback) = await _newsService.GetNewsAsync();
            NewsListBox.ItemsSource = items;
            NewsListBox.SelectedIndex = items.Count > 0 ? 0 : -1;
            NewsStatusTextBlock.Text = usedFallback ? $"Painel offline carregado em {DateTime.Now:dd/MM/yyyy HH:mm}." : $"Feed oficial carregada em {DateTime.Now:dd/MM/yyyy HH:mm}.";
        }
        finally
        {
            _isLoadingNews = false;
            RefreshNewsButton.IsEnabled = true;
        }
    }

    private void NewsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NewsListBox.SelectedItem is LauncherNewsItem item)
        {
            NewsTitleTextBlock.Text = item.Title;
            NewsMetaTextBlock.Text = item.Meta;
            NewsSummaryTextBlock.Text = item.Summary;
            OpenNewsButton.IsEnabled = !string.IsNullOrWhiteSpace(item.Url);
            return;
        }

        NewsTitleTextBlock.Text = "Selecione uma noticia";
        NewsMetaTextBlock.Text = string.Empty;
        NewsSummaryTextBlock.Text = string.Empty;
        OpenNewsButton.IsEnabled = false;
    }

    private void OpenNewsButton_Click(object sender, RoutedEventArgs e)
    {
        if (NewsListBox.SelectedItem is LauncherNewsItem item && !string.IsNullOrWhiteSpace(item.Url))
        {
            Process.Start(new ProcessStartInfo { FileName = item.Url, UseShellExecute = true });
        }
    }

    private void OpenOfficialSiteButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo { FileName = "https://www.minecraft.net/en-us/download", UseShellExecute = true });
    }
}
