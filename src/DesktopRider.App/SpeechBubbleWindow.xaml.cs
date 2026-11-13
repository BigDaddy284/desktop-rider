using System.Windows;
using System.Windows.Input;
using DesktopRider.AI;

namespace DesktopRider.App;

/// <summary>The rider's speech bubble. What it says and when it shows is decided by <see cref="SpeechBubbleViewModel"/>.</summary>
public partial class SpeechBubbleWindow : Window
{
    public SpeechBubbleWindow(SpeechBubbleViewModel bubble)
    {
        InitializeComponent();
        DataContext = bubble;
    }

    /// <summary>Raised when the user clicks the bubble.</summary>
    public event EventHandler? Clicked;

    /// <summary>Shows or hides the little tail that points down at the rider.</summary>
    public bool TailVisible
    {
        set => Tail.Visibility = value ? Visibility.Visible : Visibility.Hidden;
    }

    private void OnBubbleClick(object sender, MouseButtonEventArgs e) => Clicked?.Invoke(this, EventArgs.Empty);
}
