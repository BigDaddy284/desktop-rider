using System.Xml.Linq;

namespace DesktopRider.Core.Tests;

/// <summary>
/// The Windows build is published as a single file. Sprite sheets must stay beside the exe, not inside it,
/// or the app cannot open them (0.9.0 shipped without the larger trick sheets this way).
/// </summary>
public class PublishLayoutTests
{
    [Fact]
    public void SpriteSheets_AreKeptOutOfTheSingleFileExe()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "DesktopRider.App", "DesktopRider.App.csproj")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var project = XDocument.Load(Path.Combine(dir!.FullName, "src", "DesktopRider.App", "DesktopRider.App.csproj"));
        var sprites = project.Descendants("Content")
            .Where(c => ((string?)c.Attribute("Include") ?? "").Contains(@"Assets\Sprites", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(sprites);
        Assert.All(sprites, c =>
        {
            Assert.Equal("true", (string?)c.Attribute("ExcludeFromSingleFile"));
            Assert.Equal("PreserveNewest", (string?)c.Attribute("CopyToPublishDirectory"));
        });
    }
}
