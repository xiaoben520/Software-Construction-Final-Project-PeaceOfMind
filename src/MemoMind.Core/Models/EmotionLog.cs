namespace MemoMind.Core.Models;

public class EmotionLog
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string EmotionLabel { get; set; } = string.Empty;
    public int EmotionScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
