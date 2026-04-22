namespace MemoMind.Core.Models;

public class PomodoroSession
{
    public int Id { get; set; }
    public int? TaskId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public bool IsCompleted { get; set; }
}
