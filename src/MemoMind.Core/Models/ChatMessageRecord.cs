namespace MemoMind.Core.Models;

public class ChatMessageRecord
{
    public int Id { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Time { get; set; } = DateTime.Now;
    public bool IsUserMessage { get; set; }
}
