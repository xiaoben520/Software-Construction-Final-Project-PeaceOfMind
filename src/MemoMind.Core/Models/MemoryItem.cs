namespace MemoMind.Core.Models;

public class MemoryItem
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class ExtractedMemory
{
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
