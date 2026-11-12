using System.Windows;

namespace DesktopRider.App;

/// <summary>
/// Tells the user the result of something they chose from the menu. One window is reused and its words replaced,
/// so a reply that arrives piece by piece grows in place instead of opening window after window.
/// </summary>
public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    /// <summary>Shows the window if it is not already showing, with these words.</summary>
    public void ShowMessage(string title, string message)
    {
        Heading.Text = title;
        Body.Text = message;
        if (!IsVisible)
        {
            Show();
        }
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => Close();
}
