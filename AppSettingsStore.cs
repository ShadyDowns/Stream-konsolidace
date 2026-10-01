using System.IO;
using System.Text.Json;

namespace StreamKartoteka.Services;

public sealed record AppSettings(string Channel = "", string Login = "");

public static class AppSettingsStore
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StreamKartoteka");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    public static void Save(string channel, string login)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(
                new AppSettings(channel, login),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception)
        {
            // Settings are a convenience only; a read-only profile must not break the app.
        }
    }
}

