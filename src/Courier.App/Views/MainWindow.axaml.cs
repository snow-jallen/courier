using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Courier.App.ViewModels;

namespace Courier.App.Views;

public partial class MainWindow : Window, IFilePicker
{
    private MainWindowViewModel? _model;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public MainWindow(AppServices services) : this()
    {
        _model = new MainWindowViewModel(services, this);
        DataContext = _model;
    }

    public async Task<string?> PickPdfAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose the report you exported from LCR",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private void OnImport(object? sender, RoutedEventArgs e) => _model?.ShowImport();
    private void OnPeople(object? sender, RoutedEventArgs e) => _model?.ShowPeople();
    private void OnSend(object? sender, RoutedEventArgs e) => _model?.ShowSend();
    private void OnLcr(object? sender, RoutedEventArgs e) => _model?.ShowLcr();
    private void OnSetup(object? sender, RoutedEventArgs e) => _model?.ShowSetup();
}
