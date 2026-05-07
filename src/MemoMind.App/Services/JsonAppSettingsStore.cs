using System.IO;
using System.Text.Json;
using MemoMind.App.Models;

namespace MemoMind.App.Services;

public class JsonAppSettingsStore : IAppSettingsStore
{
    private readonly string settingsFilePath;
    private readonly JsonSerializerOptions serializerOptions = new() { WriteIndented = true };

    public JsonAppSettingsStore(string settingsFilePath)
    {
        this.settingsFilePath = settingsFilePath;
    }

    public Task<UserSettings> LoadAsync()
    {
        if (!File.Exists(settingsFilePath))
        {
            return Task.FromResult(new UserSettings());
        }

        try
        {
            var json = File.ReadAllText(settingsFilePath);
            var settings = JsonSerializer.Deserialize<UserSettings>(json);
            return Task.FromResult(settings ?? new UserSettings());
        }
        catch
        {
            return Task.FromResult(new UserSettings());
        }
    }

    public Task SaveAsync(UserSettings settings)
    {
        var directory = Path.GetDirectoryName(settingsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, serializerOptions);
        File.WriteAllText(settingsFilePath, json);
        return Task.CompletedTask;
    }
}
