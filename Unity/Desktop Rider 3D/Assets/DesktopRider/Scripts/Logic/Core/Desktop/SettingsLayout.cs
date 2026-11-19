#nullable enable

using System;
using System.Collections.Generic;

namespace DesktopRider.Core.Desktop
{
    /// <summary>A rectangle in the settings window, in device-independent pixels (96 to the inch).</summary>
    public readonly struct PanelRect
    {
        public PanelRect(double x, double y, double width, double height) { X = x; Y = y; Width = width; Height = height; }
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public double Right => X + Width;
        public double Bottom => Y + Height;
        public bool Contains(double x, double y) => x >= X && x < Right && y >= Y && y < Bottom;
        public PanelRect Scaled(double s) => new(Math.Round(X * s), Math.Round(Y * s), Math.Round(Width * s), Math.Round(Height * s));
        public override string ToString() => $"({X}, {Y}, {Width} x {Height})";
    }

    /// <summary>What is under the mouse in the settings window.</summary>
    public readonly struct PanelHit
    {
        public PanelHit(SettingRow? row, int choice, string? button) { Row = row; Choice = choice; Button = button; }
        public SettingRow? Row { get; }
        /// <summary>For a <see cref="SettingKind.Choice"/> row: which button; otherwise -1.</summary>
        public int Choice { get; }
        /// <summary>One of the buttons along the bottom (<see cref="SettingsLayout.ResetButton"/>, <see cref="SettingsLayout.TrickButton"/>,
        /// <see cref="SettingsLayout.CloseButton"/>), or null.</summary>
        public string? Button { get; }
        public bool IsEmpty => Row == null && Button == null;
    }

    /// <summary>
    /// Where everything goes in the settings window, worked out without drawing anything so it can be tested: a title,
    /// then each heading with its lines (title and one or two lines of description on the left, the control on the right),
    /// then three buttons along the bottom. Everything is in device-independent pixels; the window scales it for the screen.
    /// The window can be resized: the lines stretch with its width, and when it is too short for the descriptions they are
    /// left out (<see cref="ShowsDescriptions"/>), leaving the headings, names, controls and buttons (<see cref="Fit"/>).
    /// </summary>
    public sealed class SettingsLayout
    {
        public const string ResetButton = "reset";
        public const string TrickButton = "trick";
        public const string CloseButton = "close";
        public const string ResetText = "Move to corner";
        public const string TrickText = "Do a trick";
        public const string CloseText = "Close";

        /// <summary>The width the window opens at; <see cref="MinWidth"/> and <see cref="MaxWidth"/> are the limits when resized.</summary>
        public const double Width = 560, MinWidth = 440, MaxWidth = 900;
        public const double Margin = 24, TitleHeight = 56, SectionHeight = 34, RowHeight = 68, CompactRowHeight = 44, FooterHeight = 68;
        public const double ResetWidth = 150, TrickWidth = 110, CloseWidth = 96, ButtonGap = 12;
        public const double ToggleWidth = 44, ToggleHeight = 22, ChoiceWidth = 84, ChoiceHeight = 30, SliderWidth = 200, Gap = 16;

        private readonly Dictionary<SettingRow, PanelRect> _rows = new();
        private readonly Dictionary<SettingRow, PanelRect> _controls = new();

        /// <param name="width">The window's inside width (kept within <see cref="MinWidth"/> and <see cref="MaxWidth"/>).</param>
        /// <param name="showDescriptions">False for the short form: each line is just its name and control.</param>
        public SettingsLayout(SettingsPanel panel, double width = Width, bool showDescriptions = true)
        {
            Panel = panel ?? throw new ArgumentNullException(nameof(panel));
            PanelWidth = Math.Max(MinWidth, Math.Min(MaxWidth, width));
            ShowsDescriptions = showDescriptions;
            double rowHeight = showDescriptions ? RowHeight : CompactRowHeight, panelWidth = PanelWidth;
            double y = TitleHeight;
            var headings = new List<(string, PanelRect)>();
            foreach (var section in panel.Sections)
            {
                headings.Add((section, new PanelRect(Margin, y, panelWidth - (2 * Margin), SectionHeight)));
                y += SectionHeight;
                foreach (var row in panel.Rows)
                {
                    if (row.Section != section) continue;
                    var area = new PanelRect(Margin, y, panelWidth - (2 * Margin), rowHeight);
                    _rows[row] = area;
                    double w = ControlWidth(row), h = row.Kind == SettingKind.Toggle ? ToggleHeight : ChoiceHeight;
                    double lift = row.Kind == SettingKind.Slider && showDescriptions ? 6 : 0;   // room for the end labels
                    _controls[row] = new PanelRect(area.Right - w, area.Y + ((rowHeight - h) / 2) - lift, w, h);
                    y += rowHeight;
                }
            }
            Headings = headings;
            y += 8;
            Height = y + FooterHeight;
            double by = y + ((FooterHeight - 32) / 2);
            Close = new PanelRect(panelWidth - Margin - CloseWidth, by, CloseWidth, 32);
            Reset = new PanelRect(Margin, by, ResetWidth, 32);
            Trick = new PanelRect(Reset.Right + ButtonGap, by, TrickWidth, 32);
            FooterLine = y;
        }

        /// <summary>
        /// The layout for a window whose inside is <paramref name="width"/> by <paramref name="height"/>: with descriptions
        /// when they fit, otherwise the short form.
        /// </summary>
        public static SettingsLayout Fit(SettingsPanel panel, double width, double height)
        {
            var full = new SettingsLayout(panel, width, true);
            return full.Height <= height + 0.5 ? full : new SettingsLayout(panel, width, false);
        }

        /// <summary>The smallest inside size the window can be made: the short form at <see cref="MinWidth"/>.</summary>
        public static (double Width, double Height) SmallestSize(SettingsPanel panel) => (MinWidth, new SettingsLayout(panel, MinWidth, false).Height);

        /// <summary>The tallest the window needs to be: everything shown. Taller would only add empty space.</summary>
        public static double FullHeight(SettingsPanel panel) => new SettingsLayout(panel).Height;

        public SettingsPanel Panel { get; }
        /// <summary>The window's inside width this layout is for.</summary>
        public double PanelWidth { get; }
        /// <summary>False in the short form, where only the names are written beside the controls.</summary>
        public bool ShowsDescriptions { get; }
        public double Height { get; }
        public IReadOnlyList<(string Text, PanelRect Area)> Headings { get; }
        public PanelRect Close { get; }
        public PanelRect Reset { get; }
        public PanelRect Trick { get; }
        /// <summary>Where the thin line above the buttons goes.</summary>
        public double FooterLine { get; }
        public PanelRect TitleArea => new(Margin, 14, PanelWidth - (2 * Margin), TitleHeight - 18);

        public static double ControlWidth(SettingRow row) => row.Kind switch
        {
            SettingKind.Toggle => ToggleWidth,
            SettingKind.Choice => ChoiceWidth * Math.Max(1, row.Choices.Count),
            _ => SliderWidth,
        };

        /// <summary>The whole line, words and control.</summary>
        public PanelRect RowArea(SettingRow row) => _rows[row];

        /// <summary>The control: the switch, the row of choice buttons or the slider's track area.</summary>
        public PanelRect Control(SettingRow row) => _controls[row];

        /// <summary>Where the title and description go: the line's left part, clear of the control.</summary>
        public PanelRect TextArea(SettingRow row)
        {
            var area = _rows[row];
            if (!ShowsDescriptions) return new PanelRect(area.X, area.Y + ((area.Height - 20) / 2), area.Width - ControlWidth(row) - Gap, 20);
            return new PanelRect(area.X, area.Y + 10, area.Width - ControlWidth(row) - Gap, area.Height - 14);
        }

        /// <summary>One button of a choice row.</summary>
        public PanelRect ChoiceButton(SettingRow row, int index)
        {
            var c = _controls[row];
            return new PanelRect(c.X + (index * ChoiceWidth), c.Y, ChoiceWidth, c.Height);
        }

        /// <summary>The words under the two ends of a slider.</summary>
        public PanelRect SliderLabels(SettingRow row)
        {
            var c = _controls[row];
            return new PanelRect(c.X, c.Bottom, c.Width, 18);
        }

        /// <summary>Where along the slider (0 to 1) the row's value sits.</summary>
        public static double SliderFraction(SettingRow row) =>
            row.Max > row.Min ? Math.Max(0, Math.Min(1, (row.Value - row.Min) / (row.Max - row.Min))) : 0;

        /// <summary>The value a slider takes with the mouse at <paramref name="x"/>, kept within its ends.</summary>
        public double SliderValueAt(SettingRow row, double x)
        {
            var c = _controls[row];
            const double knob = 10;
            double f = (x - c.X - knob) / Math.Max(1, c.Width - (2 * knob));
            f = Math.Max(0, Math.Min(1, f));
            return row.Min + (f * (row.Max - row.Min));
        }

        /// <summary>What is at a point: a control (anywhere on its line counts for a switch), a choice button, or a bottom button.</summary>
        public PanelHit HitTest(double x, double y)
        {
            if (Close.Contains(x, y)) return new PanelHit(null, -1, CloseButton);
            if (Reset.Contains(x, y)) return new PanelHit(null, -1, ResetButton);
            if (Trick.Contains(x, y)) return new PanelHit(null, -1, TrickButton);
            foreach (var row in Panel.Rows)
            {
                var c = _controls[row];
                switch (row.Kind)
                {
                    case SettingKind.Toggle:
                        if (_rows[row].Contains(x, y)) return new PanelHit(row, -1, null);
                        break;
                    case SettingKind.Choice:
                        for (int i = 0; i < row.Choices.Count; i++)
                            if (ChoiceButton(row, i).Contains(x, y)) return new PanelHit(row, i, null);
                        break;
                    default:
                        if (new PanelRect(c.X - 4, c.Y - 6, c.Width + 8, c.Height + 12).Contains(x, y)) return new PanelHit(row, -1, null);
                        break;
                }
            }
            return default;
        }
    }
}
