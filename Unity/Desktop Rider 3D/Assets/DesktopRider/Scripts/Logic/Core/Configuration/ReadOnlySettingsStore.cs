#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DesktopRider.Core.Infrastructure;
using Newtonsoft.Json;

namespace DesktopRider.Core.Configuration
{
    /// <summary>
    /// The settings as a companion window sees them: read from the main program's file, never written, and never moved
    /// aside. A file that cannot be read just now (the main program may be saving it) leaves the last settings read in use.
    /// </summary>
    public sealed class ReadOnlySettingsStore : ISettingsStore
    {
        private readonly string _file;
        private AppSettings? _last;

        public ReadOnlySettingsStore(string file)
        {
            Guard.NotBlank(file, nameof(file));
            _file = file;
        }

        public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string text;
                using (var stream = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true))
                using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                {
                    text = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                var read = JsonText.Read<AppSettings>(text, JsonText.LocalFileSettings);
                if (read != null)
                {
                    _last = SettingsNormalizer.Normalize(read);
                    return new SettingsLoadResult(_last, SettingsLoadStatus.Loaded);
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                // Kept as it was; the next change to the file is read again.
            }

            return new SettingsLoadResult(_last ?? SettingsNormalizer.Normalize(null), _last != null ? SettingsLoadStatus.Loaded : SettingsLoadStatus.Defaults);
        }

        /// <summary>Does nothing: only the main program saves the settings.</summary>
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
