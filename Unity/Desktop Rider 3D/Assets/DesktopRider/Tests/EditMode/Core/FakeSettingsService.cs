#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DesktopRider.Core.Configuration;

namespace DesktopRider.Core.Tests
{
    /// <summary>Settings held in memory, with a switch to make saving fail.</summary>
    internal sealed class FakeSettingsService : ISettingsService
    {
        public event EventHandler? Changed;

        public AppSettings Current { get; } = new();

        public SettingsLoadStatus LoadStatus => SettingsLoadStatus.Defaults;

        public bool FailSaves { get; set; }

        public int SaveCount { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(Action<AppSettings> change, CancellationToken cancellationToken = default)
        {
            change(Current);
            if (FailSaves)
            {
                return Task.FromException(new IOException("The disk is full."));
            }

            SaveCount++;
            Changed?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }
}
