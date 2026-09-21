using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Courier.App.ViewModels;

namespace Courier.App.Views;

public partial class PeopleView : UserControl
{
    public PeopleView() => AvaloniaXamlLoader.Load(this);

    /// <summary>Opening a row by double-clicking it is what people try first; the Edit
    /// button stays for anyone who does not.</summary>
    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: PersonRow row })
        {
            row.EditCommand.Execute(null);
            e.Handled = true;
        }
    }
}
