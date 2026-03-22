using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Bn_s_Launcher.Models;
using Bn_s_Launcher.Services;

namespace Bn_s_Launcher;

public partial class VersionBrowserWindow : Window
{
    private readonly VersionCatalogService _versionCatalogService = new();
    private List<MinecraftVersionEntry> _allVersions = [];
    private bool _isInitializingWindow = true;
    private bool _isViewReady;

    public VersionBrowserWindow(string? selectedVersionId = null)
    {
        try
        {
            InitializeComponent();
            SelectedVersionId = selectedVersionId ?? string.Empty;
            _isViewReady = true;
        }
        finally
        {
            _isInitializingWindow = false;
        }
    }

    public string SelectedVersionId { get; private set; }

    public string SelectedVersionType { get; private set; } = "release";

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadVersionsAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadVersionsAsync();
    }

    private async System.Threading.Tasks.Task LoadVersionsAsync()
    {
        StatusTextBlock.Text = "Atualizando catalogo oficial...";
        SelectButton.IsEnabled = false;

        try
        {
            _allVersions = (await _versionCatalogService.GetVersionsAsync()).ToList();
            ApplyFilters();
            StatusTextBlock.Text = $"{_allVersions.Count} versoes encontradas.";
        }
        catch (System.Exception ex)
        {
            StatusTextBlock.Text = $"Falha ao carregar versoes: {ex.Message}";
            VersionsListView.ItemsSource = null;
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isViewReady || _isInitializingWindow)
        {
            return;
        }

        ApplyFilters();
    }

    private void FilterCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isViewReady || _isInitializingWindow)
        {
            return;
        }

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (!_isViewReady || _isInitializingWindow ||
            SearchTextBox is null ||
            VersionsListView is null ||
            FilterSummaryTextBlock is null ||
            SelectedVersionTextBlock is null ||
            SelectedVersionMetaTextBlock is null ||
            SelectButton is null)
        {
            return;
        }

        IEnumerable<MinecraftVersionEntry> filtered = _allVersions;
        var search = SearchTextBox.Text?.Trim() ?? string.Empty;

        filtered = filtered.Where(version =>
            IsTypeEnabled(version.Type) &&
            (string.IsNullOrWhiteSpace(search) || version.Id.Contains(search, System.StringComparison.OrdinalIgnoreCase)));

        var filteredList = filtered.ToList();
        VersionsListView.ItemsSource = filteredList;
        FilterSummaryTextBlock.Text = $"{filteredList.Count} versoes exibidas.";

        var selected = filteredList.FirstOrDefault(version => version.Id == SelectedVersionId);
        if (selected is not null)
        {
            VersionsListView.SelectedItem = selected;
        }
        else if (filteredList.Count > 0)
        {
            VersionsListView.SelectedIndex = 0;
        }
        else
        {
            SelectedVersionTextBlock.Text = "Nenhuma versao encontrada";
            SelectedVersionMetaTextBlock.Text = "Ajuste os filtros para encontrar outra versao.";
            SelectButton.IsEnabled = false;
        }
    }

    private bool IsTypeEnabled(string type)
    {
        return type switch
        {
            "release" => ReleaseCheckBox.IsChecked == true,
            "snapshot" => SnapshotCheckBox.IsChecked == true,
            "old_beta" => BetaCheckBox.IsChecked == true,
            "old_alpha" => AlphaCheckBox.IsChecked == true,
            _ => true
        };
    }

    private void VersionsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isViewReady || _isInitializingWindow || SelectedVersionTextBlock is null || SelectedVersionMetaTextBlock is null || SelectButton is null)
        {
            return;
        }

        if (VersionsListView.SelectedItem is not MinecraftVersionEntry version)
        {
            SelectButton.IsEnabled = false;
            return;
        }

        SelectedVersionTextBlock.Text = version.Id;
        SelectedVersionMetaTextBlock.Text = $"{version.Type} | {version.ReleaseLabel} | {version.Badge}";
        SelectButton.IsEnabled = true;
    }

    private void VersionsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        CommitSelection();
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        CommitSelection();
    }

    private void CommitSelection()
    {
        if (VersionsListView.SelectedItem is not MinecraftVersionEntry version)
        {
            return;
        }

        SelectedVersionId = version.Id;
        SelectedVersionType = version.Type;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
            Close();
        }
    }

    private void HeaderBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        DragMove();
    }
}
