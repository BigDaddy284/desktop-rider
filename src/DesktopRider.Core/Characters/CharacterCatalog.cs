using System.Text.Json;
using DesktopRider.Core.Configuration;

namespace DesktopRider.Core.Characters;

/// <summary>A character the user can choose: the folder its pictures are in and the name shown for it.</summary>
/// <param name="Id">The folder name, which is what is saved in the settings.</param>
/// <param name="Name">The name shown in settings.</param>
public sealed record CharacterInfo(string Id, string Name);

/// <summary>The characters that can live on the desktop.</summary>
public interface ICharacterCatalog
{
    /// <summary>Every character whose pictures are installed, the standard rider first.</summary>
    IReadOnlyList<CharacterInfo> Available { get; }

    /// <summary>The folder of pictures to show for a saved choice. Falls back to the standard rider if that choice is not installed.</summary>
    string FolderFor(string? id);
}

/// <summary>
/// Finds the characters installed beside the application: every folder under the pictures folder with a sprite list
/// in it. Nothing outside that folder is read.
/// </summary>
public sealed class CharacterCatalog : ICharacterCatalog
{
    private readonly string _root;
    private readonly Lazy<IReadOnlyList<CharacterInfo>> _available;

    public CharacterCatalog(string spritesFolder)
    {
        _root = spritesFolder ?? throw new ArgumentNullException(nameof(spritesFolder));
        _available = new Lazy<IReadOnlyList<CharacterInfo>>(Find);
    }

    public IReadOnlyList<CharacterInfo> Available => _available.Value;

    public string FolderFor(string? id)
    {
        var chosen = Available.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
                     ?? Available.FirstOrDefault(c => c.Id == AppearanceSettings.DefaultCharacter);
        return Path.Combine(_root, chosen?.Id ?? AppearanceSettings.DefaultCharacter);
    }

    private List<CharacterInfo> Find()
    {
        var found = new List<CharacterInfo>();
        if (!Directory.Exists(_root))
        {
            return found;
        }

        foreach (var folder in Directory.EnumerateDirectories(_root).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var id = Path.GetFileName(folder);
            var list = Path.Combine(folder, "sprites.json");
            if (!File.Exists(list))
            {
                continue;
            }

            found.Add(new CharacterInfo(id, NameIn(list) ?? id));
        }

        // The standard rider always comes first.
        return [.. found.OrderBy(c => c.Id == AppearanceSettings.DefaultCharacter ? 0 : 1)];
    }

    private static string? NameIn(string list)
    {
        try
        {
            using var stream = File.OpenRead(list);
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "displayName", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                {
                    var name = property.Value.GetString()?.Trim();
                    return string.IsNullOrEmpty(name) ? null : name;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // An unreadable list still offers the character under its folder name; loading it reports the problem.
        }

        return null;
    }
}
