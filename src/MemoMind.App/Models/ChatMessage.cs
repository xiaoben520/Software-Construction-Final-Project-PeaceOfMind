namespace MemoMind.App.Models;

public class ChatMessage
{
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Time { get; set; } = DateTime.Now;
}
