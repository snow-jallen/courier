using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Courier.App.ViewModels;

namespace Courier.App.Views;

public partial class MainWindow : Window, IFilePicker, IClipboardWriter, IDatabasePicker
{
    private MainWindowViewModel? _model;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public MainWindow(AppServices services) : this()
    {
        _model = new MainWindowViewModel(services, this, this);
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

    private static FilePickerFileType CourierDatabase =>
        new("Courier directory") { Patterns = ["*.db"] };

    public async Task<string?> PickExistingAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a Courier directory",
            AllowMultiple = false,
            FileTypeFilter = [CourierDatabase],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickNewAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Where should the new directory be kept?",
            SuggestedFileName = "contacts.db",
            DefaultExtension = "db",
            FileTypeChoices = [CourierDatabase],
            ShowOverwritePrompt = true,
        });
        return file?.TryGetLocalPath();
    }

    public async Task CopyAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null) await clipboard.SetTextAsync(text);
    }

    private void OnImport(object? sender, RoutedEventArgs e) => _model?.ShowImport();
    private async void OnPeople(object? sender, RoutedEventArgs e) => await Show(m => m.ShowPeopleAsync());
    private async void OnSend(object? sender, RoutedEventArgs e) => await Show(m => m.ShowSendAsync());
    private async void OnLcr(object? sender, RoutedEventArgs e) => await Show(m => m.ShowLcrAsync());
    private async void OnHistory(object? sender, RoutedEventArgs e) => await Show(m => m.ShowHistoryAsync());
    private void OnSetup(object? sender, RoutedEventArgs e) => _model?.ShowSetup();

    /// <summary>A screen that cannot load is not a reason to take the whole window
    /// down, which is what an unhandled exception in an async void handler would do.</summary>
    private async Task Show(Func<MainWindowViewModel, Task> open)
    {
        if (_model is null) return;
        try
        {
            await open(_model);
        }
        catch (Exception failure)
        {
            _model.Current = new ScreenFailedViewModel(failure.Message);
        }
    }
}
