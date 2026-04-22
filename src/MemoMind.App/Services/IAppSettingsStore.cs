using MemoMind.App.Models;

namespace MemoMind.App.Services;

public interface IAppSettingsStore
{
    Task<UserSettings> LoadAsync();
    Task SaveAsync(UserSettings settings);
}
