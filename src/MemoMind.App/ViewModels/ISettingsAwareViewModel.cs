using MemoMind.App.Models;

namespace MemoMind.App.ViewModels;

public interface ISettingsAwareViewModel
{
    void ApplySettings(UserSettings settings);
}