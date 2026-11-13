using System.Windows;
using System.Windows.Media;
using DesktopRider.Desktop;

namespace DesktopRider.App;

/// <summary>
/// A second see-through window that shows the animations too big for the rider's own window, such as jumps.
/// It is drawn exactly over the rider, so the rider appears to stay where it is while ramps come and go around it.
/// The rider's own window keeps its size and place, so nothing that depends on it (dragging, the saved position,
/// speech bubbles) is affected.
/// </summary>
public partial class StageWindow : Window
{
    public StageWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ClickThroughWindow.Apply(this);
    }

    /// <summary>Shows one frame.</summary>
    /// <param name="frame">The picture.</param>
    /// <param name="size">How big to draw it, in screen units.</param>
    /// <param name="corner">Where its top-left corner goes on the screen.</param>
    /// <param name="opacity">The same see-through setting as the rider.</param>
    public void Present(ImageSource frame, Size size, Point corner, double opacity)
    {
        StageImage.Source = frame;
        StageImage.Width = size.Width;
        StageImage.Height = size.Height;
        StageImage.Opacity = opacity;
        MoveTo(corner);
        if (!IsVisible)
        {
            Show();
        }
    }

    public void MoveTo(Point corner)
    {
        Left = corner.X;
        Top = corner.Y;
    }
}
