#nullable enable

using System;
using System.Collections.Generic;
using DesktopRider.Core.ViewModels;

namespace DesktopRider.Core.Desktop
{
    /// <summary>
    /// The "Do a trick" line in the tray menu: asks the rider for a trick now. The rider finds room for it, riding to a
    /// clear spot first if it has to. Greyed-out text is not possible in this menu, so the line is left out while
    /// tricks are not available (no trick props installed).
    /// </summary>
    public sealed class TrickMenu : ITrayMenuContributor
    {
        public const string MenuText = "Do a trick";

        private readonly Func<bool> _available;

        /// <param name="available">Whether tricks can be done at all; asked each time the menu opens.</param>
        public TrickMenu(Func<bool> available) => _available = available ?? throw new ArgumentNullException(nameof(available));

        /// <summary>Raised when the line is chosen. Handlers run on the thread that runs the menu command.</summary>
        public event EventHandler? Requested;

        public IReadOnlyList<TrayMenuEntry> BuildEntries() =>
            _available()
                ? new[] { new TrayMenuEntry(MenuText, new RelayCommand(() => Requested?.Invoke(this, EventArgs.Empty))) }
                : Array.Empty<TrayMenuEntry>();
    }
}
