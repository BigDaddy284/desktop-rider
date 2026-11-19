#nullable enable

using System;
using System.IO;

namespace DesktopRider.Core.Infrastructure
{
    internal static class FileMoves
    {
        /// <summary>
        /// Moves a file, replacing anything already at the destination.
        /// (.NET Standard 2.1 has no <c>File.Move</c> that overwrites.)
        /// </summary>
        public static void MoveOverwriting(string source, string destination)
        {
            if (File.Exists(destination))
            {
                try
                {
                    // Swaps the files in one step, so a crash can never leave the destination missing.
                    File.Replace(source, destination, null);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    File.Delete(destination);
                }
            }

            File.Move(source, destination);
        }
    }
}
