using System.Windows;
using System.Windows.Controls;
using Folio.ViewModels;

namespace Folio.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnConfirmSecurity(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.ConfirmSecurity(Pw1.Password, Pw2.Password);
            Pw1.Clear();
            Pw2.Clear();
        }
    }
}
