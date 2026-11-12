using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopRider.AI;

namespace DesktopRider.App;

/// <summary>
/// The compact conversation window. All of its behaviour lives in <see cref="ConversationViewModel"/>;
/// this class only handles the keyboard, keeps the newest words in view and hides instead of closing
/// so the conversation is still there when it is opened again.
/// </summary>
public partial class ChatWindow : Window
{
    private readonly ConversationViewModel _conversation;
    private bool _followNewWords = true;

    public ChatWindow(ConversationViewModel conversation)
    {
        InitializeComponent();
        _conversation = conversation;
        DataContext = conversation;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    /// <summary>Raised when the user asks to type in a bubble beside the rider instead of this window.</summary>
    public event EventHandler? BubblesRequested;

    /// <summary>Puts the typing cursor in the message box.</summary>
    public void FocusInput()
    {
        Activate();
        Input.Focus();
        Keyboard.Focus(Input);
    }

    /// <summary>Set when Desktop Rider is exiting, so the window really closes instead of being put away.</summary>
    internal bool ClosingForGood { get; set; }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Alt+F4 puts the window away like the X does, so the conversation is still there next time.
        if (!ClosingForGood)
        {
            e.Cancel = true;
            Dispatcher.BeginInvoke(Hide);
        }

        base.OnClosing(e);
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            _followNewWords = true;
            History.ScrollToEnd();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Hide();

    private void OnBubblesClick(object sender, RoutedEventArgs e) => BubblesRequested?.Invoke(this, EventArgs.Empty);

    // Opens the list of AI models as a menu in the same style as the rider's own menu.
    private void OnModelClick(object sender, RoutedEventArgs e)
    {
        if (_conversation.Models.Count == 0)
        {
            // Nothing to choose from yet: look again, which also explains what is wrong if Ollama cannot be reached.
            _conversation.CheckConnectionCommand.Execute(null);
            return;
        }

        var menu = new ContextMenu { PlacementTarget = ModelButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var model in _conversation.Models)
        {
            var item = new MenuItem
            {
                Header = model.Replace("_", "__", StringComparison.Ordinal),
                IsChecked = string.Equals(model, _conversation.SelectedModel, StringComparison.Ordinal),
            };
            item.Click += (_, _) => _conversation.SelectedModel = model;
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }

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

    // Keeps the newest words in view while a reply grows, unless the user has scrolled up to read something older.
    private void OnHistoryScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange == 0)
        {
            _followNewWords = History.VerticalOffset >= History.ScrollableHeight - 2;
        }
        else if (_followNewWords)
        {
            History.ScrollToEnd();
        }
    }
}
