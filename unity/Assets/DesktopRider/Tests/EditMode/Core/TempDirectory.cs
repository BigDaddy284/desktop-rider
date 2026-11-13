#nullable enable

using System;
using System.IO;

namespace DesktopRider.Core.Tests
{
    /// <summary>A unique scratch folder that is removed when the test finishes.</summary>
    public sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DesktopRiderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A leftover scratch folder must not fail a test.
            }
        }
    }
}
