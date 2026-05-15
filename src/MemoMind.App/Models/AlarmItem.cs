namespace MemoMind.App.Models;

public enum AlarmRepeatMode
{
    Once,
    Daily,
    Weekly,
    CustomDays
}

public class AlarmItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = string.Empty;
    public int Hour { get; set; }
    public int Minute { get; set; }
    public AlarmRepeatMode RepeatMode { get; set; } = AlarmRepeatMode.Once;
    public bool Monday { get; set; }
    public bool Tuesday { get; set; }
    public bool Wednesday { get; set; }
    public bool Thursday { get; set; }
    public bool Friday { get; set; }
    public bool Saturday { get; set; }
    public bool Sunday { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime LastTriggered { get; set; } = DateTime.MinValue;
}
