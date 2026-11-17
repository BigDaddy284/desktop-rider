using DesktopRider.Core.Characters;
using DesktopRider.Core.Configuration;

namespace DesktopRider.Core.Tests;

public class CharacterCatalogTests
{
    [Fact]
    public void Finds_every_folder_with_a_sprite_list_standard_rider_first()
    {
        using var temp = new TempDirectory();
        Add(temp, "Sidecar41", """{ "displayName": "Sidecar 41", "frameWidth": 1 }""");
        Add(temp, "Rider", """{ "displayName": "Harvey" }""");
        Directory.CreateDirectory(Path.Combine(temp.Path, "Empty"));

        var catalog = new CharacterCatalog(temp.Path);

        Assert.Equal([new CharacterInfo("Rider", "Harvey"), new CharacterInfo("Sidecar41", "Sidecar 41")], catalog.Available);
    }

    [Fact]
    public void A_list_without_a_name_or_that_cannot_be_read_uses_the_folder_name()
    {
        using var temp = new TempDirectory();
        Add(temp, "Rider", "{ }");
        Add(temp, "Broken", "not json");

        var catalog = new CharacterCatalog(temp.Path);

        Assert.Equal(["Rider", "Broken"], catalog.Available.Select(c => c.Name));
    }

    [Fact]
    public void Shows_the_chosen_character_or_the_standard_rider_if_it_is_not_installed()
    {
        using var temp = new TempDirectory();
        Add(temp, "Rider", "{ }");
        Add(temp, "Sidecar41", "{ }");
        var catalog = new CharacterCatalog(temp.Path);

        Assert.Equal(Path.Combine(temp.Path, "Sidecar41"), catalog.FolderFor("sidecar41"));
        Assert.Equal(Path.Combine(temp.Path, "Rider"), catalog.FolderFor("Gone"));
        Assert.Equal(Path.Combine(temp.Path, "Rider"), catalog.FolderFor(null));
    }

    [Fact]
    public void A_missing_pictures_folder_offers_nothing_and_falls_back_to_the_rider()
    {
        var root = Path.Combine(Path.GetTempPath(), "DesktopRiderTests", "missing-" + Guid.NewGuid().ToString("N"));
        var catalog = new CharacterCatalog(root);

        Assert.Empty(catalog.Available);
        Assert.Equal(Path.Combine(root, AppearanceSettings.DefaultCharacter), catalog.FolderFor("Sidecar41"));
    }

    [Theory]
    [InlineData(null, "Rider")]
    [InlineData("", "Rider")]
    [InlineData("  Sidecar41 ", "Sidecar41")]
    [InlineData("..\\..\\Windows", "Rider")]
    [InlineData("a/b", "Rider")]
    [InlineData("Sidecar_41-b", "Sidecar_41-b")]
    public void Settings_keep_only_a_plain_folder_name_as_the_character(string? saved, string kept)
    {
        var settings = new AppSettings();
        settings.Appearance.Character = saved!;

        Assert.Equal(kept, SettingsNormalizer.Normalize(settings).Appearance.Character);
    }

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(5.0, 2.0)]
    [InlineData(0.0, 0.25)]
    [InlineData(double.NaN, 1.0)]
    public void Settings_keep_the_riding_speed_in_range(double saved, double kept)
    {
        var settings = new AppSettings();
        settings.Behaviour.RidingSpeed = saved;

        Assert.Equal(kept, SettingsNormalizer.Normalize(settings).Behaviour.RidingSpeed);
    }

    private static void Add(TempDirectory temp, string id, string json)
    {
        var folder = Path.Combine(temp.Path, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "sprites.json"), json);
    }
}
