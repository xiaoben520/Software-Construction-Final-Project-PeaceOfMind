namespace MemoMind.App.Models;

public class RecentFileEntry
{
    public string DisplayName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public DateTime LastOpenedAt { get; set; }
    public string Icon => IsFolder ? "\U0001F4C1" : "\U0001F4C4";
}
