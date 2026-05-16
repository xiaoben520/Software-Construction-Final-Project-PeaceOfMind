using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using MemoMind.App.Commands;
using MemoMind.App.Models;
using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MemoMind.App.ViewModels;

public class TaskBoardViewModel : ViewModelBase
{
    private readonly ITaskService taskService;
    private readonly DispatcherTimer countdownTimer;
    private string newTaskTitle = string.Empty;
    private string newTaskDescription = string.Empty;
    private DateTime? newTaskStartDate;
    private DateTime? newTaskDueDate;
    private bool newTaskIsUrgent;
    private string statusMessage = "已连接本地数据库，任务会自动保存。";
    private string currentDate = DateTime.Now.ToString("yyyy年M月d日");
    private string currentDayOfWeek = DateTime.Now.ToString("dddd");
    private string currentTime = DateTime.Now.ToString("HH:mm");
    private string statusFilter = "全部";
    private bool isFilterVisible;
    private bool isCreatePanelVisible;
    private bool isCalendarVisible;
    private DateTime calendarMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
    private ObservableCollection<CalendarDay> calendarDays = new();
    private DateTime? selectedCalendarDate;
    private bool isEventInputVisible;
    private string newEventText = string.Empty;
    private Dictionary<DateTime, string> events = new();
    private TaskItem? selectedTask;
    private bool isEditing;
    private string editTitle = string.Empty;
    private string editDescription = string.Empty;
    private DateTime? editStartDate;
    private DateTime? editDueDate;
    private bool editIsUrgent;
    private string editStatus = "Todo";
    private int newTaskEstimatedHours;
    private int newTaskEstimatedMinutes;
    private int editEstimatedHours;
    private int editEstimatedMinutes;

    public TaskBoardViewModel()
        : this(App.Services.GetRequiredService<ITaskService>())
    {
    }

    public TaskBoardViewModel(ITaskService taskService)
    {
        this.taskService = taskService;
        Tasks = new ObservableCollection<TaskItem>();
        FilteredTasks = new ObservableCollection<TaskItem>();
        StatusFilterOptions = ["全部", "Todo", "Doing", "Done"];

        AddTaskCommand = new RelayCommand(_ => AddTask(), _ => CanAddTask());
        DeleteTaskCommand = new RelayCommand(_ => DeleteTask(), _ => SelectedTask is not null);
        CompleteTaskCommand = new RelayCommand(p => CompleteTask(p as TaskItem));
        StartTaskCommand = new RelayCommand(p => ConfirmStart(p as TaskItem));
        PauseTaskCommand = new RelayCommand(p => PauseTask(p as TaskItem));
        ToggleUrgentCommand = new RelayCommand(_ => ToggleUrgent(), _ => SelectedTask is not null);
        StartEditCommand = new RelayCommand(_ => StartEdit(), _ => SelectedTask is not null);
        SaveEditCommand = new RelayCommand(_ => SaveEdit(), _ => CanSaveEdit());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        CancelCreateCommand = new RelayCommand(_ => CancelCreate());
        ToggleCreatePanelCommand = new RelayCommand(_ => ToggleCreatePanel());
        ToggleFilterCommand = new RelayCommand(_ => ToggleFilter());
        FilterCommand = new RelayCommand(p => ApplyFilter(p?.ToString() ?? "全部"));
        OpenCalendarCommand = new RelayCommand(_ => OpenCalendar());
        CloseCalendarCommand = new RelayCommand(_ => CloseCalendar());
        SelectCalendarDayCommand = new RelayCommand(p => SelectCalendarDay(p));
        ShowEventInputCommand = new RelayCommand(_ => ShowEventInput(), _ => selectedCalendarDate.HasValue);
        SaveEventCommand = new RelayCommand(_ => SaveEvent(), _ => selectedCalendarDate.HasValue);
        PrevMonthCommand = new RelayCommand(_ => PrevMonth());
        NextMonthCommand = new RelayCommand(_ => NextMonth());
        NextMonthCommand = new RelayCommand(_ => NextMonth());

        _ = LoadTasksAsync();

        countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        countdownTimer.Tick += (s, e) =>
        {
            var now = DateTime.Now;
            CurrentDate = now.ToString("yyyy年M月d日");
            CurrentDayOfWeek = now.ToString("dddd");
            CurrentTime = now.ToString("HH:mm");
            UpdateTodaySummary();

            var reapplyFilter = false;
            foreach (var task in Tasks)
            {
                if (task.Status != "Doing") continue;
                if (task.CountdownEndTime <= DateTime.MinValue) continue;
                var secs = (int)(task.CountdownEndTime - now).TotalSeconds;
                if (secs <= 0)
                {
                    if (task.IsBreakTime)
                    {
                        task.Status = "Todo";
                        task.IsBreakTime = false;
                        task.CountdownEndTime = DateTime.MinValue;
                        task.CountdownDisplay = string.Empty;
                        task.CountdownProgress = 0;
                        task.CountdownStatusText = string.Empty;
                        reapplyFilter = true;
                    }
                    else
                    {
                        task.IsBreakTime = true;
                        task.CountdownPhaseSeconds = 300;
                        task.CountdownEndTime = DateTime.Now.AddMinutes(5);
                    }
                }
                else
                {
                    var m = secs / 60;
                    var sec = secs % 60;
                    task.CountdownDisplay = $"{m:D2}:{sec:D2}";
                    task.CountdownProgress = task.CountdownPhaseSeconds > 0
                        ? (double)secs / task.CountdownPhaseSeconds
                        : 0;
                    task.CountdownStatusText = task.IsBreakTime ? "休息中" : "进行中";
                }
            }
            if (reapplyFilter) ApplyFilter();
        };
        countdownTimer.Start();
    }

    public ObservableCollection<TaskItem> Tasks { get; }
    public ObservableCollection<TaskItem> FilteredTasks { get; }
    public ObservableCollection<string> StatusFilterOptions { get; }

    public string StatusMessage
    {
        get => statusMessage;
        set { statusMessage = value; OnPropertyChanged(); }
    }

    public string NewTaskTitle
    {
        get => newTaskTitle;
        set
        {
            newTaskTitle = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsUrgentEnabled));
            (AddTaskCommand as RelayCommand)?.RaiseCanExecuteChanged();
            if (!IsUrgentEnabled) NewTaskIsUrgent = false;
        }
    }

    public string NewTaskDescription
    {
        get => newTaskDescription;
        set
        {
            newTaskDescription = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsUrgentEnabled));
            if (!IsUrgentEnabled) NewTaskIsUrgent = false;
        }
    }

    public DateTime? NewTaskStartDate
    {
        get => newTaskStartDate;
        set { newTaskStartDate = value; OnPropertyChanged(); RaiseAddCanExecuteChanged(); }
    }

    public DateTime? NewTaskDueDate
    {
        get => newTaskDueDate;
        set { newTaskDueDate = value; OnPropertyChanged(); RaiseAddCanExecuteChanged(); }
    }

    public bool NewTaskIsUrgent
    {
        get => newTaskIsUrgent;
        set { newTaskIsUrgent = value; OnPropertyChanged(); }
    }

    public bool IsUrgentEnabled => !string.IsNullOrWhiteSpace(NewTaskTitle) || !string.IsNullOrWhiteSpace(NewTaskDescription);

    public string StatusFilter
    {
        get => statusFilter;
        set { statusFilter = value; OnPropertyChanged(); }
    }

    public TaskItem? SelectedTask
    {
        get => selectedTask;
        set
        {
            selectedTask = value;
            OnPropertyChanged();
            (DeleteTaskCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ToggleUrgentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (StartEditCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool IsEditing
    {
        get => isEditing;
        set { isEditing = value; OnPropertyChanged(); }
    }

    public string EditTitle
    {
        get => editTitle;
        set { editTitle = value; OnPropertyChanged(); RaiseSaveCanExecuteChanged(); }
    }

    public string EditDescription
    {
        get => editDescription;
        set { editDescription = value; OnPropertyChanged(); }
    }

    public DateTime? EditStartDate
    {
        get => editStartDate;
        set { editStartDate = value; OnPropertyChanged(); RaiseSaveCanExecuteChanged(); }
    }

    public DateTime? EditDueDate
    {
        get => editDueDate;
        set { editDueDate = value; OnPropertyChanged(); RaiseSaveCanExecuteChanged(); }
    }

    public bool EditIsUrgent
    {
        get => editIsUrgent;
        set { editIsUrgent = value; OnPropertyChanged(); }
    }

    public string EditStatus
    {
        get => editStatus;
        set { editStatus = value; OnPropertyChanged(); }
    }

    public int NewTaskEstimatedHours
    {
        get => newTaskEstimatedHours;
        set { newTaskEstimatedHours = value; OnPropertyChanged(); RaiseAddCanExecuteChanged(); }
    }

    public int NewTaskEstimatedMinutes
    {
        get => newTaskEstimatedMinutes;
        set { newTaskEstimatedMinutes = value; OnPropertyChanged(); RaiseAddCanExecuteChanged(); }
    }

    public int EditEstimatedHours
    {
        get => editEstimatedHours;
        set { editEstimatedHours = value; OnPropertyChanged(); RaiseSaveCanExecuteChanged(); }
    }

    public int EditEstimatedMinutes
    {
        get => editEstimatedMinutes;
        set { editEstimatedMinutes = value; OnPropertyChanged(); RaiseSaveCanExecuteChanged(); }
    }

    public ICommand AddTaskCommand { get; }
    public ICommand DeleteTaskCommand { get; }
    public ICommand CompleteTaskCommand { get; }
    public ICommand StartTaskCommand { get; }
    public ICommand PauseTaskCommand { get; }
    public ICommand ToggleUrgentCommand { get; }
    public ICommand StartEditCommand { get; }
    public ICommand SaveEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand CancelCreateCommand { get; }
    public ICommand ToggleCreatePanelCommand { get; }
    public ICommand ToggleFilterCommand { get; }
    public ICommand FilterCommand { get; }
    public ICommand OpenCalendarCommand { get; }
    public ICommand CloseCalendarCommand { get; }
    public ICommand SelectCalendarDayCommand { get; }
    public ICommand ShowEventInputCommand { get; }
    public ICommand SaveEventCommand { get; }
    public ICommand PrevMonthCommand { get; }
    public ICommand NextMonthCommand { get; }

    public bool IsCreatePanelVisible
    {
        get => isCreatePanelVisible;
        set { isCreatePanelVisible = value; OnPropertyChanged(); }
    }

    public bool IsFilterVisible
    {
        get => isFilterVisible;
        set { isFilterVisible = value; OnPropertyChanged(); }
    }

    public bool IsCalendarVisible
    {
        get => isCalendarVisible;
        set { isCalendarVisible = value; OnPropertyChanged(); }
    }

    public string CalendarTitle => $"{calendarMonth.Year}年{calendarMonth.Month}月";

    public ObservableCollection<CalendarDay> CalendarDays => calendarDays;

    public bool IsEventInputVisible
    {
        get => isEventInputVisible;
        set { isEventInputVisible = value; OnPropertyChanged(); }
    }

    public string NewEventText
    {
        get => newEventText;
        set { newEventText = value; OnPropertyChanged(); (SaveEventCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public string CurrentDate
    {
        get => currentDate;
        set { currentDate = value; OnPropertyChanged(); }
    }

    public string CurrentDayOfWeek
    {
        get => currentDayOfWeek;
        set { currentDayOfWeek = value; OnPropertyChanged(); }
    }

    public string CurrentTime
    {
        get => currentTime;
        set { currentTime = value; OnPropertyChanged(); }
    }

    private string todaySummary = string.Empty;
    public string TodaySummary
    {
        get => todaySummary;
        set { todaySummary = value; OnPropertyChanged(); }
    }

    private void UpdateTodaySummary()
    {
        var today = DateTime.Today;
        Holidays2026.TryGetValue((today.Month, today.Day), out var holiday);
        var hasHoliday = today.Year == 2026 && holiday != null;
        var hasEvent = events.TryGetValue(today, out var evt);

        if (hasHoliday && hasEvent && !string.IsNullOrWhiteSpace(evt))
            TodaySummary = $"今天是{holiday}，今日日程：{evt}";
        else if (hasHoliday)
            TodaySummary = $"今天是{holiday}";
        else if (hasEvent && !string.IsNullOrWhiteSpace(evt))
            TodaySummary = $"今日日程：{evt}";
        else
            TodaySummary = "今日无事发生";
    }

    public static IReadOnlyList<int> HourOptions { get; } = [0, 1, 2, 3, 4, 5, 6, 7, 8];
    public static IReadOnlyList<int> MinuteOptions { get; } = [0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55];

    private bool CanAddTask() =>
        !string.IsNullOrWhiteSpace(NewTaskTitle)
        && (NewTaskStartDate.HasValue || NewTaskDueDate.HasValue)
        && (NewTaskEstimatedHours > 0 || NewTaskEstimatedMinutes > 0);

    private bool CanSaveEdit() =>
        !string.IsNullOrWhiteSpace(EditTitle)
        && (EditStartDate.HasValue || EditDueDate.HasValue)
        && (EditEstimatedHours > 0 || EditEstimatedMinutes > 0);

    private void RaiseAddCanExecuteChanged() => (AddTaskCommand as RelayCommand)?.RaiseCanExecuteChanged();
    private void RaiseSaveCanExecuteChanged() => (SaveEditCommand as RelayCommand)?.RaiseCanExecuteChanged();

    private async Task LoadTasksAsync()
    {
        var tasks = await taskService.GetAllAsync();
        Tasks.Clear();
        foreach (var taskItem in tasks
            .OrderBy(task => !task.IsUrgent)
            .ThenBy(task => task.Status switch { "Doing" => 0, "Todo" => 1, _ => 2 })
            .ThenBy(task => task.DueDate ?? DateTime.MaxValue))
        {
            Tasks.Add(taskItem);
        }
        ApplyFilter();
        StatusMessage = $"已从数据库加载 {Tasks.Count} 条任务。";
    }

    private async void AddTask()
    {
        var taskItem = new TaskItem
        {
            Title = NewTaskTitle.Trim(),
            Description = NewTaskDescription.Trim(),
            StartDate = NewTaskStartDate,
            DueDate = NewTaskDueDate,
            IsUrgent = NewTaskIsUrgent,
            Status = "Todo",
            SourceType = "Manual",
            EstimatedHours = NewTaskEstimatedHours,
            EstimatedMinutes = NewTaskEstimatedMinutes
        };

        await taskService.AddAsync(taskItem);
        Tasks.Insert(0, taskItem);
        ApplyFilter();
        StatusMessage = "任务已保存到本地数据库。";

        NewTaskTitle = string.Empty;
        NewTaskDescription = string.Empty;
        NewTaskStartDate = null;
        NewTaskDueDate = null;
        NewTaskIsUrgent = false;
        NewTaskEstimatedHours = 0;
        NewTaskEstimatedMinutes = 0;
        IsCreatePanelVisible = false;
    }

    private async void DeleteTask()
    {
        if (SelectedTask is null) return;
        var id = SelectedTask.Id;
        await taskService.DeleteAsync(id);
        Tasks.Remove(SelectedTask);
        ApplyFilter();
        SelectedTask = null;
        StatusMessage = "任务已删除。";
    }

    private async void CompleteTask(TaskItem? task)
    {
        if (task is null || task.Status == "Done") return;
        task.Status = "Done";
        task.CompletedAt = DateTime.Now;
        task.CountdownDisplay = string.Empty;
        task.CountdownProgress = 0;
        task.CountdownStatusText = string.Empty;
        task.IsBreakTime = false;
        await taskService.UpdateAsync(task);
        ApplyFilter();
        SelectedTask = task;
        StatusMessage = "任务已标记为完成。";
    }

    private async void ConfirmStart(TaskItem? task)
    {
        if (task is null || task.Status == "Doing") return;
        var totalSeconds = task.EstimatedHours * 3600 + task.EstimatedMinutes * 60;
        task.Status = "Doing";
        task.CompletedAt = null;
        task.IsBreakTime = false;
        task.CountdownPhaseSeconds = totalSeconds;
        task.CountdownEndTime = DateTime.Now.AddSeconds(totalSeconds);
        await taskService.UpdateAsync(task);
        ApplyFilter();
        SelectedTask = task;
        StatusMessage = $"任务已开工 (预计 {task.EstimatedHours}h{task.EstimatedMinutes:D2}m)。";
    }

    private async void PauseTask(TaskItem? task)
    {
        if (task is null || task.Status != "Doing") return;
        task.Status = "Todo";
        task.CompletedAt = null;
        task.CountdownDisplay = string.Empty;
        task.CountdownProgress = 0;
        task.CountdownStatusText = string.Empty;
        task.IsBreakTime = false;
        await taskService.UpdateAsync(task);
        ApplyFilter();
        SelectedTask = task;
        StatusMessage = "任务已暂停。";
    }

    private async void ToggleUrgent()
    {
        if (SelectedTask is null) return;
        SelectedTask.IsUrgent = !SelectedTask.IsUrgent;
        await taskService.UpdateAsync(SelectedTask);
        var selectedId = SelectedTask.Id;
        await LoadTasksAsync();
        SelectedTask = Tasks.FirstOrDefault(t => t.Id == selectedId);
        StatusMessage = SelectedTask?.IsUrgent == true ? "任务已标记为紧急。" : "任务已取消紧急。";
    }

    private void StartEdit()
    {
        if (SelectedTask is null) return;
        EditTitle = SelectedTask.Title;
        EditDescription = SelectedTask.Description;
        EditStartDate = SelectedTask.StartDate;
        EditDueDate = SelectedTask.DueDate;
        EditIsUrgent = SelectedTask.IsUrgent;
        EditStatus = SelectedTask.Status;
        EditEstimatedHours = SelectedTask.EstimatedHours;
        EditEstimatedMinutes = SelectedTask.EstimatedMinutes;
        IsEditing = true;
    }

    private async void SaveEdit()
    {
        if (SelectedTask is null) return;
        SelectedTask.Title = EditTitle.Trim();
        SelectedTask.Description = EditDescription.Trim();
        SelectedTask.StartDate = EditStartDate;
        SelectedTask.DueDate = EditDueDate;
        SelectedTask.IsUrgent = EditIsUrgent;
        SelectedTask.Status = EditStatus;
        SelectedTask.EstimatedHours = EditEstimatedHours;
        SelectedTask.EstimatedMinutes = EditEstimatedMinutes;
        await taskService.UpdateAsync(SelectedTask);
        IsEditing = false;
        EditTitle = string.Empty;
        EditDescription = string.Empty;
        EditStartDate = null;
        EditDueDate = null;
        EditIsUrgent = false;
        EditStatus = "Todo";
        EditEstimatedHours = 0;
        EditEstimatedMinutes = 0;
        ApplyFilter();
        StatusMessage = "任务已更新。";
    }

    private void CancelEdit()
    {
        IsEditing = false;
        EditTitle = string.Empty;
        EditDescription = string.Empty;
        EditStartDate = null;
        EditDueDate = null;
        EditIsUrgent = false;
        EditStatus = "Todo";
        EditEstimatedHours = 0;
        EditEstimatedMinutes = 0;
    }

    private void CancelCreate()
    {
        NewTaskTitle = string.Empty;
        NewTaskDescription = string.Empty;
        NewTaskStartDate = null;
        NewTaskDueDate = null;
        NewTaskIsUrgent = false;
        NewTaskEstimatedHours = 0;
        NewTaskEstimatedMinutes = 0;
        IsCreatePanelVisible = false;
    }

    private void ToggleCreatePanel()
    {
        if (IsFilterVisible) IsFilterVisible = false;
        if (IsEditing) IsEditing = false;
        IsCreatePanelVisible = !IsCreatePanelVisible;
    }

    private void ToggleFilter()
    {
        if (IsCreatePanelVisible) IsCreatePanelVisible = false;
        if (IsEditing) IsEditing = false;
        IsFilterVisible = !IsFilterVisible;
    }

    private void OpenCalendar()
    {
        calendarMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        BuildCalendar();
        OnPropertyChanged(nameof(CalendarTitle));
        IsCalendarVisible = true;
    }

    private void CloseCalendar()
    {
        IsCalendarVisible = false;
        IsEventInputVisible = false;
        selectedCalendarDate = null;
    }

    private void SelectCalendarDay(object? parameter)
    {
        if (parameter is not CalendarDay day || !day.IsCurrentMonth) return;
        selectedCalendarDate = day.Date;
        foreach (var d in calendarDays) d.IsSelected = false;
        day.IsSelected = true;
        (ShowEventInputCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void ShowEventInput()
    {
        IsEventInputVisible = !IsEventInputVisible;
        if (IsEventInputVisible)
        {
            if (selectedCalendarDate.HasValue && events.TryGetValue(selectedCalendarDate.Value.Date, out var existing))
                NewEventText = existing;
            else
                NewEventText = string.Empty;
        }
    }

    private void SaveEvent()
    {
        if (selectedCalendarDate is null) return;
        var key = selectedCalendarDate.Value.Date;
        var text = NewEventText.Trim();
        if (string.IsNullOrWhiteSpace(text))
            events.Remove(key);
        else
            events[key] = text;
        BuildCalendar();
        UpdateTodaySummary();
        IsEventInputVisible = false;
        NewEventText = string.Empty;
    }

    private void PrevMonth()
    {
        calendarMonth = calendarMonth.AddMonths(-1);
        BuildCalendar();
        OnPropertyChanged(nameof(CalendarTitle));
    }

    private void NextMonth()
    {
        calendarMonth = calendarMonth.AddMonths(1);
        BuildCalendar();
        OnPropertyChanged(nameof(CalendarTitle));
    }

    private static readonly Dictionary<(int Month, int Day), string> Holidays2026 = new()
    {
        [(1, 1)] = "元旦",
        [(1, 28)] = "除夕",
        [(1, 29)] = "春节",
        [(1, 30)] = "初二",
        [(1, 31)] = "初三",
        [(2, 14)] = "情人节",
        [(3, 8)] = "妇女节",
        [(3, 12)] = "植树节",
        [(4, 5)] = "清明节",
        [(5, 1)] = "劳动节",
        [(5, 4)] = "青年节",
        [(5, 10)] = "母亲节",
        [(6, 1)] = "儿童节",
        [(6, 19)] = "端午节",
        [(6, 21)] = "父亲节",
        [(7, 1)] = "建党节",
        [(8, 1)] = "建军节",
        [(9, 10)] = "教师节",
        [(9, 25)] = "中秋节",
        [(10, 1)] = "国庆节",
        [(10, 2)] = "国庆",
        [(10, 3)] = "国庆",
        [(12, 25)] = "圣诞节",
    };

    private void BuildCalendar()
    {
        calendarDays.Clear();
        var firstDayOfWeek = (int)calendarMonth.DayOfWeek;
        var daysInMonth = DateTime.DaysInMonth(calendarMonth.Year, calendarMonth.Month);
        var today = DateTime.Today;

        for (int i = 0; i < firstDayOfWeek; i++)
            calendarDays.Add(new CalendarDay { Day = 0, IsCurrentMonth = false });

        for (int d = 1; d <= daysInMonth; d++)
        {
            var isToday = calendarMonth.Year == today.Year && calendarMonth.Month == today.Month && d == today.Day;
            var holiday = string.Empty;
            if (calendarMonth.Year == 2026)
                Holidays2026.TryGetValue((calendarMonth.Month, d), out holiday);

            var date = new DateTime(calendarMonth.Year, calendarMonth.Month, d);
            events.TryGetValue(date, out var eventText);

            calendarDays.Add(new CalendarDay
            {
                Day = d,
                IsCurrentMonth = true,
                IsToday = isToday,
                IsSelected = selectedCalendarDate.HasValue && selectedCalendarDate.Value.Date == date,
                HolidayText = holiday ?? string.Empty,
                EventText = eventText ?? string.Empty,
                Date = date
            });
        }
    }

    private void ApplyFilter(string? status = null)
    {
        if (status is not null) StatusFilter = status;
        var filtered = StatusFilter switch
        {
            "Todo" => Tasks.Where(t => t.Status == "Todo"),
            "Doing" => Tasks.Where(t => t.Status == "Doing"),
            "Done" => Tasks.Where(t => t.Status == "Done"),
            _ => Tasks.AsEnumerable()
        };

        FilteredTasks.Clear();
        foreach (var task in filtered
            .OrderBy(t => !t.IsUrgent)
            .ThenBy(t => t.Status switch { "Doing" => 0, "Todo" => 1, _ => 2 })
            .ThenBy(t => t.DueDate ?? DateTime.MaxValue))
        {
            FilteredTasks.Add(task);
        }

        StatusMessage = $"显示 {FilteredTasks.Count} 条任务。";
    }
}
