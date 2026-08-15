using System.Windows;
using System.Windows.Input;
using Folio.Services.Persistence;

namespace Folio.Views;

public partial class UnlockWindow : Window
{
    private readonly IPortfolioStore _store;

    public UnlockWindow(IPortfolioStore store)
    {
        InitializeComponent();
        _store = store;
        Loaded += (_, _) => PasswordBox.Focus();
    }

    private void OnUnlock(object sender, RoutedEventArgs e)
    {
        if (_store.Unlock(PasswordBox.Password))
        {
            DialogResult = true;
            Close();
            return;
        }

        ErrorText.Text = "Wrong password. Try again.";
        ErrorText.Visibility = Visibility.Visible;
        PasswordBox.Clear();
        PasswordBox.Focus();
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnUnlock(sender, e);
        }
    }
}
