using System.Windows;

namespace Folio.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        StateChanged += (_, _) => UpdateMaxRestoreGlyph();
        UpdateMaxRestoreGlyph();
    }

    private void UpdateMaxRestoreGlyph()
    {
        var maximized = WindowState == WindowState.Maximized;
        MaxGlyph.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreGlyph.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
        Padding = maximized ? new Thickness(7) : new Thickness(0);
    }

    private void OnMinimize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
