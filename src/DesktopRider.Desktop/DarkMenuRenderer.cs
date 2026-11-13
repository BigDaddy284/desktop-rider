using System.Drawing;
using System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;

namespace DesktopRider.Desktop;

/// <summary>
/// Draws the notification-area menu in the same dark style as the rider's own menu and Windows 11's menus,
/// instead of the light grey that Windows Forms uses by default.
/// </summary>
internal sealed class DarkMenuRenderer : Forms.ToolStripProfessionalRenderer
{
    public static readonly Color Surface = Color.FromArgb(0x2C, 0x2C, 0x2C);
    public static readonly Color Hover = Color.FromArgb(0x3D, 0x3D, 0x3D);
    public static readonly Color Edge = Color.FromArgb(0x45, 0x45, 0x45);
    public static readonly Color Ink = Color.White;

    public DarkMenuRenderer()
        : base(new Colours())
    {
        RoundedEdges = false;
    }

    /// <summary>Gives a menu, and every menu that opens from it, the dark look.</summary>
    public static void Apply(Forms.ToolStripDropDown menu, Forms.ToolStripRenderer renderer)
    {
        menu.Renderer = renderer;
        menu.BackColor = Surface;
        menu.ForeColor = Ink;
        foreach (Forms.ToolStripItem item in menu.Items)
        {
            item.ForeColor = Ink;
            item.Padding = new Forms.Padding(0, 3, 0, 3);
            if (item is Forms.ToolStripMenuItem { HasDropDownItems: true } parent)
            {
                Apply(parent.DropDown, renderer);
            }
        }
    }

    protected override void OnRenderArrow(Forms.ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Ink;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e)
    {
        // The standard tick is black on a pale box, which cannot be seen on a dark menu.
        var box = e.ImageRectangle;
        var size = Math.Min(box.Width, box.Height);
        float left = box.Left + ((box.Width - size) / 2f);
        float top = box.Top + ((box.Height - size) / 2f);
        var state = e.Graphics.Save();
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Ink, Math.Max(1.6f, size / 9f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        e.Graphics.DrawLines(
            pen,
            [
                new PointF(left + (size * 0.22f), top + (size * 0.52f)),
                new PointF(left + (size * 0.42f), top + (size * 0.72f)),
                new PointF(left + (size * 0.80f), top + (size * 0.30f)),
            ]);
        e.Graphics.Restore(state);
    }

    private sealed class Colours : Forms.ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Surface;

        public override Color ImageMarginGradientBegin => Surface;

        public override Color ImageMarginGradientMiddle => Surface;

        public override Color ImageMarginGradientEnd => Surface;

        public override Color MenuBorder => Edge;

        public override Color MenuItemBorder => Hover;

        public override Color MenuItemSelected => Hover;

        public override Color MenuItemSelectedGradientBegin => Hover;

        public override Color MenuItemSelectedGradientEnd => Hover;

        public override Color MenuItemPressedGradientBegin => Hover;

        public override Color MenuItemPressedGradientMiddle => Hover;

        public override Color MenuItemPressedGradientEnd => Hover;

        public override Color SeparatorDark => Edge;

        public override Color SeparatorLight => Edge;

        public override Color CheckBackground => Surface;

        public override Color CheckSelectedBackground => Hover;

        public override Color CheckPressedBackground => Hover;
    }
}
