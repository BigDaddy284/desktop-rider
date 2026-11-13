using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopRider.AI;

namespace DesktopRider.App;

/// <summary>
/// The rider's settings window. All of its behaviour lives in <see cref="RiderSettingsViewModel"/>; this class
/// only handles dragging, the keyboard and hiding instead of closing so it opens instantly next time.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(RiderSettingsViewModel settings)
    {
        InitializeComponent();
        DataContext = settings;
    }

    /// <summary>Set when Desktop Rider is exiting, so the window really closes instead of being put away.</summary>
    internal bool ClosingForGood { get; set; }

    /// <summary>Shows the window and brings it to the front.</summary>
    public void Open()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        KeepWhatWasTyped();

        // Alt+F4 puts the window away like the X does.
        if (!ClosingForGood)
        {
            e.Cancel = true;
            Dispatcher.BeginInvoke(Hide);
        }

        base.OnClosing(e);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        // Clicking away from the window keeps what was being typed, the same as moving to another box does.
        KeepWhatWasTyped();
        base.OnDeactivated(e);
    }

    private void PutAway()
    {
        KeepWhatWasTyped();
        Hide();
    }

    // Typed text is normally saved when the typing cursor leaves its box. This saves it wherever the cursor is.
    private void KeepWhatWasTyped()
    {
        if (Keyboard.FocusedElement is TextBox box && ReferenceEquals(GetWindow(box), this))
        {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => PutAway();

    private void OnHeadingMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    // Enter in the "remember this" box adds it, the same as the Add button.
    private void OnNewNoteKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return && DataContext is RiderSettingsViewModel settings)
        {
            e.Handled = true;
            settings.AddNoteCommand.Execute(null);
        }
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            PutAway();
        }
    }
}
