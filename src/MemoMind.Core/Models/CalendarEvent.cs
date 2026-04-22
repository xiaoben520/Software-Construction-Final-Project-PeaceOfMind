namespace MemoMind.Core.Models;

public class CalendarEvent
{
    public int Id { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int? RelatedTaskId { get; set; }
    public string Notes { get; set; } = string.Empty;
}
