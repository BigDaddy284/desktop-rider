using System.Windows.Threading;
using DesktopRider.Core;
using DesktopRider.Core.ViewModels;
using Microsoft.Extensions.Logging;
using Forms = System.Windows.Forms;

namespace DesktopRider.Desktop;

/// <summary>
/// Puts Desktop Rider's icon in the notification area beside the clock. Everything it shows and does is decided by
/// <see cref="TrayViewModel"/>; this class only carries it out. Nothing here runs unless the user touches the icon.
/// </summary>
public sealed partial class TrayIconController : IDisposable
{
    private const int HintMilliseconds = 8000;

    // Windows allows at most 127 characters here on older builds; keep well inside it.
    private const int MaxToolTipLength = 63;

    private readonly TrayViewModel _viewModel;
    private readonly ILogger<TrayIconController> _logger;
    private readonly Dispatcher _dispatcher;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly System.Drawing.Icon _image;
    private readonly bool _ownsImage;
    private bool _disposed;

    /// <summary>Must be created on the interface thread. A null picture means the standard Windows application icon is used.</summary>
    public TrayIconController(TrayViewModel viewModel, System.Drawing.Icon? image, ILogger<TrayIconController> logger)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dispatcher = Dispatcher.CurrentDispatcher;
        _ownsImage = image is not null;
        _image = image ?? System.Drawing.SystemIcons.Application;

        _menu = new Forms.ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = true };
        _menu.Opening += OnMenuOpening;

        _icon = new Forms.NotifyIcon
        {
            Icon = _image,
            Text = AppInfo.Name,
            ContextMenuStrip = _menu,
        };
        _icon.MouseClick += OnIconClick;
        _icon.BalloonTipClicked += OnHintClicked;
        _viewModel.HintRequested += OnHintRequested;
    }

    /// <summary>Makes the icon appear.</summary>
    public void Show()
    {
        try
        {
            RefreshToolTip();
            _icon.Visible = true;
            LogShown(_logger);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Without the icon the rider's own right-click menu still works, so carry on.
            LogFailed(_logger, ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewModel.HintRequested -= OnHintRequested;
        _icon.MouseClick -= OnIconClick;
        _icon.BalloonTipClicked -= OnHintClicked;
        _menu.Opening -= OnMenuOpening;

        // Hiding first removes the icon straight away instead of leaving a ghost until the mouse passes over it.
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        if (_ownsImage)
        {
            _image.Dispose();
        }
    }

    private void OnIconClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            _viewModel.DefaultCommand.Execute(null);
        }
    }

    private void OnHintClicked(object? sender, EventArgs e) => _viewModel.HintClickedCommand.Execute(null);

    private void OnMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // The menu is rebuilt each time it opens, so it always matches the current state without any background updating.
        RefreshToolTip();
        foreach (var old in _menu.Items.Cast<Forms.ToolStripItem>().ToList())
        {
            old.Dispose();
        }

        _menu.Items.Clear();
        foreach (var entry in _viewModel.BuildMenu())
        {
            if (entry.IsSeparator)
            {
                _menu.Items.Add(new Forms.ToolStripSeparator());
                continue;
            }

            var command = entry.Command!;
            var item = new Forms.ToolStripMenuItem(entry.Text) { Checked = entry.IsChecked };
            item.Click += (_, _) => command.Execute(null);
            _menu.Items.Add(item);
        }

        e.Cancel = false;
    }

    private void OnHintRequested(object? sender, TrayHintEventArgs e) =>
        _dispatcher.BeginInvoke(() =>
        {
            if (!_disposed && _icon.Visible)
            {
                RefreshToolTip();
                _icon.ShowBalloonTip(HintMilliseconds, e.Title, e.Message, Forms.ToolTipIcon.None);
            }
        });

    private void RefreshToolTip()
    {
        var text = _viewModel.ToolTip;
        _icon.Text = text.Length > MaxToolTipLength ? text[..MaxToolTipLength] : text;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The notification-area icon is showing.")]
    private static partial void LogShown(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The notification-area icon could not be shown. The rider's own menu still works.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
