using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MemoMind.App.Models;

public class CalendarDay : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public int Day { get; set; }
    public bool IsCurrentMonth { get; set; }
    public bool IsToday { get; set; }

    private bool isSelected;
    public bool IsSelected
    {
        get => isSelected;
        set { isSelected = value; OnPropertyChanged(); }
    }

    public string HolidayText { get; set; } = string.Empty;

    private string eventText = string.Empty;
    public string EventText
    {
        get => eventText;
        set { eventText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasEvent)); }
    }

    public bool HasHoliday => !string.IsNullOrEmpty(HolidayText);
    public bool HasEvent => !string.IsNullOrEmpty(EventText);
    public string Display => Day > 0 ? Day.ToString() : string.Empty;
    public DateTime Date { get; set; }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
