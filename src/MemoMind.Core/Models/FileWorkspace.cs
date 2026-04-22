namespace MemoMind.Core.Models;

public class FileWorkspace
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTime? LastOpenedAt { get; set; }
}
