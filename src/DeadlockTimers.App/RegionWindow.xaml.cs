using System.Windows;
using System.Windows.Input;

namespace DeadlockTimers.App;

/// <summary>
/// Interaction logic for RegionWindow.xaml
/// </summary>
public partial class RegionWindow : Window
{
    public RegionWindow()
    {
        InitializeComponent();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }
}
