using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using DesktopRider.AI;

namespace DesktopRider.App;

/// <summary>
/// The typing bubble below the rider, used when the user has chosen "Chat in bubbles". It shares the conversation
/// with the conversation window, so switching between the two loses nothing.
/// </summary>
public partial class InputBubbleWindow : Window
{
    private readonly ConversationViewModel _conversation;

    public InputBubbleWindow(ConversationViewModel conversation)
    {
        InitializeComponent();
        _conversation = conversation;
        DataContext = conversation;
    }

    /// <summary>Raised when the user asks for the conversation window instead of bubbles.</summary>
    public event EventHandler? WindowRequested;

    /// <summary>Set when Desktop Rider is exiting, so the window really closes instead of being put away.</summary>
    internal bool ClosingForGood { get; set; }

    /// <summary>Shows or hides the little tail that points up at the rider.</summary>
    public bool TailVisible
    {
        set => Tail.Visibility = value ? Visibility.Visible : Visibility.Hidden;
    }

    /// <summary>Puts the typing cursor in the message box.</summary>
    public void FocusInput()
    {
        Activate();
        Input.Focus();
        Keyboard.Focus(Input);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Alt+F4 puts the bubble away like Esc does.
        if (!ClosingForGood)
        {
            e.Cancel = true;
            Dispatcher.BeginInvoke(Hide);
        }

        base.OnClosing(e);
    }

    private void OnWindowClick(object sender, RoutedEventArgs e) => WindowRequested?.Invoke(this, EventArgs.Empty);

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Hide();
        }
    }

    // Enter sends. Shift+Enter starts a new line.
    private void OnInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            _conversation.SendCommand.Execute(null);
        }
    }
}
