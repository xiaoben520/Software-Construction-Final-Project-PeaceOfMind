using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace MemoMind.Core.Models;

public class TaskItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    private bool isUrgent;
    public bool IsUrgent
    {
        get => isUrgent;
        set { isUrgent = value; OnPropertyChanged(); }
    }

    private string status = "Todo";
    public string Status
    {
        get => status;
        set { status = value; OnPropertyChanged(); }
    }

    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public string SourceType { get; set; } = "Manual";

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    private int estimatedHours = 1;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int EstimatedHours
    {
        get => estimatedHours;
        set { estimatedHours = value; OnPropertyChanged(); OnPropertyChanged(nameof(EstimatedTimeDisplay)); }
    }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    private int estimatedMinutes;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int EstimatedMinutes
    {
        get => estimatedMinutes;
        set { estimatedMinutes = value; OnPropertyChanged(); OnPropertyChanged(nameof(EstimatedTimeDisplay)); }
    }

    [NotMapped]
    public string EstimatedTimeDisplay => EstimatedHours > 0 || EstimatedMinutes > 0
        ? $"预计 {EstimatedHours}h{EstimatedMinutes:D2}m"
        : string.Empty;

    [NotMapped]
    public DateTime CountdownEndTime { get; set; }

    [NotMapped]
    private bool isBreakTime;
    [NotMapped]
    public bool IsBreakTime
    {
        get => isBreakTime;
        set { isBreakTime = value; OnPropertyChanged(); }
    }

    [NotMapped]
    private string countdownDisplay = string.Empty;
    [NotMapped]
    public string CountdownDisplay
    {
        get => countdownDisplay;
        set { countdownDisplay = value; OnPropertyChanged(); }
    }

    [NotMapped]
    private double countdownProgress;
    [NotMapped]
    public double CountdownProgress
    {
        get => countdownProgress;
        set { countdownProgress = value; OnPropertyChanged(); }
    }

    [NotMapped]
    private string countdownStatusText = string.Empty;
    [NotMapped]
    public string CountdownStatusText
    {
        get => countdownStatusText;
        set { countdownStatusText = value; OnPropertyChanged(); }
    }

    [NotMapped]
    public int CountdownPhaseSeconds { get; set; }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        try { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
        catch { }
    }
}
