using System.Collections.ObjectModel;
using System.Windows.Input;
using MemoMind.App.Commands;
using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MemoMind.App.ViewModels;

public class TaskBoardViewModel : ViewModelBase
{
    private readonly ITaskService taskService;
    private string newTaskTitle = string.Empty;
    private DateTime? newTaskDueDate = DateTime.Today;
    private string newTaskPriority = "中";
    private string statusMessage = "已连接本地数据库，任务会自动保存。";
    private string statusFilter = "全部";
    private TaskItem? selectedTask;
    private bool isEditing;
    private string editTitle = string.Empty;
    private string editDescription = string.Empty;
    private DateTime? editDueDate;
    private string editPriority = "中";
    private string editStatus = "Todo";

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

        AddTaskCommand = new RelayCommand(_ => AddTask(), _ => !string.IsNullOrWhiteSpace(NewTaskTitle));
        DeleteTaskCommand = new RelayCommand(_ => DeleteTask(), _ => SelectedTask is not null);
        CompleteTaskCommand = new RelayCommand(_ => CompleteTask(), _ => SelectedTask is not null && SelectedTask.Status != "Done");
        StartEditCommand = new RelayCommand(_ => StartEdit(), _ => SelectedTask is not null);
        SaveEditCommand = new RelayCommand(_ => SaveEdit());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        FilterCommand = new RelayCommand(_ => ApplyFilter());

        _ = LoadTasksAsync();
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
        set { newTaskTitle = value; OnPropertyChanged(); (AddTaskCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public DateTime? NewTaskDueDate
    {
        get => newTaskDueDate;
        set { newTaskDueDate = value; OnPropertyChanged(); }
    }

    public string NewTaskPriority
    {
        get => newTaskPriority;
        set { newTaskPriority = value; OnPropertyChanged(); }
    }

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
            (CompleteTaskCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
        set { editTitle = value; OnPropertyChanged(); }
    }

    public string EditDescription
    {
        get => editDescription;
        set { editDescription = value; OnPropertyChanged(); }
    }

    public DateTime? EditDueDate
    {
        get => editDueDate;
        set { editDueDate = value; OnPropertyChanged(); }
    }

    public string EditPriority
    {
        get => editPriority;
        set { editPriority = value; OnPropertyChanged(); }
    }

    public string EditStatus
    {
        get => editStatus;
        set { editStatus = value; OnPropertyChanged(); }
    }

    public ICommand AddTaskCommand { get; }
    public ICommand DeleteTaskCommand { get; }
    public ICommand CompleteTaskCommand { get; }
    public ICommand StartEditCommand { get; }
    public ICommand SaveEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand FilterCommand { get; }

    private async Task LoadTasksAsync()
    {
        var tasks = await taskService.GetAllAsync();
        Tasks.Clear();
        foreach (var taskItem in tasks.OrderBy(task => task.DueDate ?? DateTime.MaxValue))
        {
            Tasks.Add(taskItem);
        }
        ApplyFilter();
        StatusMessage = $"已从数据库加载 {Tasks.Count} 条任务。";
    }

    private async void AddTask()
    {
        var priority = NewTaskPriority switch
        {
            "高" => 3,
            "中" => 2,
            _ => 1
        };

        var taskItem = new TaskItem
        {
            Title = NewTaskTitle.Trim(),
            Description = "",
            DueDate = NewTaskDueDate,
            Priority = priority,
            Status = "Todo",
            SourceType = "Manual"
        };

        await taskService.AddAsync(taskItem);
        Tasks.Insert(0, taskItem);
        ApplyFilter();
        StatusMessage = "任务已保存到本地数据库。";

        NewTaskTitle = string.Empty;
        NewTaskDueDate = DateTime.Today;
        NewTaskPriority = "中";
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

    private async void CompleteTask()
    {
        if (SelectedTask is null || SelectedTask.Status == "Done") return;
        SelectedTask.Status = "Done";
        SelectedTask.CompletedAt = DateTime.Now;
        await taskService.UpdateAsync(SelectedTask);
        ApplyFilter();
        StatusMessage = "任务已标记为完成。";
    }

    private void StartEdit()
    {
        if (SelectedTask is null) return;
        EditTitle = SelectedTask.Title;
        EditDescription = SelectedTask.Description;
        EditDueDate = SelectedTask.DueDate;
        EditPriority = SelectedTask.Priority switch { 3 => "高", 2 => "中", _ => "低" };
        EditStatus = SelectedTask.Status;
        IsEditing = true;
    }

    private async void SaveEdit()
    {
        if (SelectedTask is null) return;
        SelectedTask.Title = EditTitle.Trim();
        SelectedTask.Description = EditDescription.Trim();
        SelectedTask.DueDate = EditDueDate;
        SelectedTask.Priority = EditPriority switch { "高" => 3, "中" => 2, _ => 1 };
        SelectedTask.Status = EditStatus;
        await taskService.UpdateAsync(SelectedTask);
        IsEditing = false;
        ApplyFilter();
        StatusMessage = "任务已更新。";
    }

    private void CancelEdit()
    {
        IsEditing = false;
    }

    private void ApplyFilter()
    {
        var filtered = StatusFilter switch
        {
            "Todo" => Tasks.Where(t => t.Status == "Todo"),
            "Doing" => Tasks.Where(t => t.Status == "Doing"),
            "Done" => Tasks.Where(t => t.Status == "Done"),
            _ => Tasks.AsEnumerable()
        };

        FilteredTasks.Clear();
        foreach (var task in filtered.OrderBy(t => t.DueDate ?? DateTime.MaxValue))
        {
            FilteredTasks.Add(task);
        }

        StatusMessage = $"显示 {FilteredTasks.Count} 条任务。";
    }
}
