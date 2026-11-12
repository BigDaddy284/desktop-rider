namespace DesktopRider.Core.Infrastructure;

/// <summary>Where Desktop Rider keeps its local files. Everything lives under one folder on this computer.</summary>
public sealed class AppPaths
{
    public AppPaths(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        DataDirectory = Path.GetFullPath(dataDirectory);
    }

    public string DataDirectory { get; }

    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public string LogDirectory => Path.Combine(DataDirectory, "Logs");

    /// <summary>The standard per-user location: %LOCALAPPDATA%\DesktopRider.</summary>
    public static AppPaths ForCurrentUser() =>
        new(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
            AppInfo.DataFolderName));

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
