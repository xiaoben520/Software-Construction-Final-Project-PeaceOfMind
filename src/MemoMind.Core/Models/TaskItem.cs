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
    public int EstimatedHours { get; set; } = 1;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int EstimatedMinutes { get; set; } = 0;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    private bool isTimePickerVisible;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsTimePickerVisible
    {
        get => isTimePickerVisible;
        set { isTimePickerVisible = value; OnPropertyChanged(); }
    }

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        try { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
        catch { }
    }
}
