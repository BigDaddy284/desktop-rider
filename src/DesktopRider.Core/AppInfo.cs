using System.Reflection;

namespace DesktopRider.Core;

/// <summary>Basic facts about the application that several parts of it need.</summary>
public static class AppInfo
{
    public const string Name = "Desktop Rider";

    /// <summary>Folder name used under the user's local application data.</summary>
    public const string DataFolderName = "DesktopRider";

    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(AppInfo).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";
}
