using System.Diagnostics.CodeAnalysis;
using System.Windows;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Placement;
using DesktopRider.Core.ViewModels;
using DesktopRider.Desktop;
using Microsoft.Extensions.Logging;

namespace DesktopRider.App;

/// <summary>The transparent, borderless window the character lives in.</summary>
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The placement controller is disposed when the window closes.")]
public partial class OverlayWindow : Window
{
    private readonly OverlayViewModel _viewModel;
    private readonly WindowPlacementController _placement;

    public OverlayWindow(
        OverlayViewModel viewModel,
        ISettingsService settings,
        IScreenProvider screens,
        ILogger<WindowPlacementController> placementLogger)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _placement = new WindowPlacementController(this, settings, screens, placementLogger);
        _placement.Attach();
        _viewModel.ResetPositionRequested += OnResetPositionRequested;
        _viewModel.ExitRequested += OnExitRequested;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.ResetPositionRequested -= OnResetPositionRequested;
        _viewModel.ExitRequested -= OnExitRequested;
        _placement.Dispose();
        base.OnClosed(e);
    }

    private async void OnResetPositionRequested(object? sender, EventArgs e) => await _placement.ResetPositionAsync();

    private void OnExitRequested(object? sender, EventArgs e) => Close();
}
