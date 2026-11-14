#nullable enable

using System.Collections.Generic;
using System.Windows.Input;
using DesktopRider.Core.ViewModels;

namespace DesktopRider.Core.Desktop
{
    /// <summary>
    /// Turns the menu the tray view model describes into numbered lines for a Windows pop-up menu, and remembers which
    /// number runs which command. Kept apart from the Windows calls so it can be tested.
    /// </summary>
    public sealed class TrayMenuPlan
    {
        /// <summary>One line of a pop-up menu. A line with <see cref="Children"/> opens a further menu.</summary>
        public sealed class Line
        {
            public Line(int id, string text, bool isChecked, bool isSeparator, IReadOnlyList<Line>? children)
            {
                Id = id; Text = text; IsChecked = isChecked; IsSeparator = isSeparator; Children = children;
            }

            public int Id { get; }
            public string Text { get; }
            public bool IsChecked { get; }
            public bool IsSeparator { get; }
            public IReadOnlyList<Line>? Children { get; }
        }

        /// <summary>Menu numbers start here; Windows reports 0 when the menu is closed without a choice.</summary>
        public const int FirstId = 1;

        readonly Dictionary<int, ICommand> commands = new Dictionary<int, ICommand>();
        int nextId = FirstId;

        public TrayMenuPlan(IReadOnlyList<TrayMenuEntry> entries) => Lines = Build(entries);

        public IReadOnlyList<Line> Lines { get; }

        /// <summary>Runs the command for the chosen number. Returns false for 0 (nothing chosen) or an unknown number.</summary>
        public bool Run(int id)
        {
            if (!commands.TryGetValue(id, out var command)) return false;
            command.Execute(null);
            return true;
        }

        List<Line> Build(IReadOnlyList<TrayMenuEntry> entries)
        {
            var lines = new List<Line>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry.IsSeparator) { lines.Add(new Line(0, string.Empty, false, true, null)); continue; }
                if (entry.Children is { } children) { lines.Add(new Line(0, entry.Text, entry.IsChecked, false, Build(children))); continue; }
                int id = nextId++;
                commands[id] = entry.Command!;
                lines.Add(new Line(id, entry.Text, entry.IsChecked, false, null));
            }
            return lines;
        }
    }
}
