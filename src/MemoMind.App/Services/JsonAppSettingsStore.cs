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

    public async Task<UserSettings> LoadAsync()
    {
        if (!File.Exists(settingsFilePath))
        {
            return new UserSettings();
        }

        await using var stream = File.OpenRead(settingsFilePath);
        var settings = await JsonSerializer.DeserializeAsync<UserSettings>(stream);
        return settings ?? new UserSettings();
    }

    public async Task SaveAsync(UserSettings settings)
    {
        var directory = Path.GetDirectoryName(settingsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, serializerOptions);
    }
}
