using DesktopRider.Core.Infrastructure;

namespace DesktopRider.Core.Tests;

public class AppPathsTests
{
    [Fact]
    public void Paths_AreAllInsideTheDataDirectory()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);

        Assert.Equal(Path.Combine(temp.Path, "settings.json"), paths.SettingsFile);
        Assert.Equal(Path.Combine(temp.Path, "Logs"), paths.LogDirectory);
    }

    [Fact]
    public void EnsureDirectoriesExist_CreatesThem_AndCanBeRepeated()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(Path.Combine(temp.Path, "nested", "data"));

        paths.EnsureDirectoriesExist();
        paths.EnsureDirectoriesExist();

        Assert.True(Directory.Exists(paths.DataDirectory));
        Assert.True(Directory.Exists(paths.LogDirectory));
    }

    [Fact]
    public void ForCurrentUser_UsesTheDesktopRiderFolder()
    {
        var paths = AppPaths.ForCurrentUser();

        Assert.Equal("DesktopRider", Path.GetFileName(paths.DataDirectory));
        Assert.True(Path.IsPathFullyQualified(paths.DataDirectory));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_RejectsBlankDirectory(string directory)
    {
        Assert.Throws<ArgumentException>(() => new AppPaths(directory));
    }

    [Fact]
    public void AppInfo_HasNameAndVersion()
    {
        Assert.Equal("Desktop Rider", AppInfo.Name);
        Assert.Matches(@"^\d+\.\d+\.\d+", AppInfo.Version);
    }
}
