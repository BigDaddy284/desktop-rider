#nullable enable

using System;
using System.IO;
using DesktopRider.Core.Infrastructure;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public class AppPathsTests
    {
        [Test]
        public void Paths_AreAllInsideTheDataDirectory()
        {
            using var temp = new TempDirectory();
            var paths = new AppPaths(temp.Path);

            Assert.Equal(Path.Combine(temp.Path, "settings.json"), paths.SettingsFile);
            Assert.Equal(Path.Combine(temp.Path, "Logs"), paths.LogDirectory);
        }

        [Test]
        public void EnsureDirectoriesExist_CreatesThem_AndCanBeRepeated()
        {
            using var temp = new TempDirectory();
            var paths = new AppPaths(Path.Combine(temp.Path, "nested", "data"));

            paths.EnsureDirectoriesExist();
            paths.EnsureDirectoriesExist();

            Assert.True(Directory.Exists(paths.DataDirectory));
            Assert.True(Directory.Exists(paths.LogDirectory));
        }

        [Test]
        public void ForCurrentUser_UsesTheDesktopRiderFolder()
        {
            var paths = AppPaths.ForCurrentUser();

            Assert.Equal("DesktopRider", Path.GetFileName(paths.DataDirectory));
            Assert.True(Path.IsPathFullyQualified(paths.DataDirectory));
        }
        [TestCase("")]
        [TestCase("  ")]
        public void Constructor_RejectsBlankDirectory(string directory)
        {
            Assert.Throws<ArgumentException>(() => new AppPaths(directory));
        }

        [Test]
        public void AppInfo_HasNameAndVersion()
        {
            Assert.Equal("Desktop Rider", AppInfo.Name);
            Assert.Matches(@"^\d+\.\d+\.\d+", AppInfo.Version);
        }
    }
}
