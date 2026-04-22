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

    public TaskBoardViewModel()
        : this(App.Services.GetRequiredService<ITaskService>())
    {
    }

    public TaskBoardViewModel(ITaskService taskService)
    {
        this.taskService = taskService;
        Tasks = new ObservableCollection<TaskItem>();

        AddTaskCommand = new RelayCommand(_ => AddTask(), _ => !string.IsNullOrWhiteSpace(NewTaskTitle));
        _ = LoadTasksAsync();
    }

    public ObservableCollection<TaskItem> Tasks { get; }

    public string StatusMessage
    {
        get => statusMessage;
        set
        {
            statusMessage = value;
            OnPropertyChanged();
        }
    }

    public string NewTaskTitle
    {
        get => newTaskTitle;
        set
        {
            newTaskTitle = value;
            OnPropertyChanged();
            (AddTaskCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public DateTime? NewTaskDueDate
    {
        get => newTaskDueDate;
        set
        {
            newTaskDueDate = value;
            OnPropertyChanged();
        }
    }

    public string NewTaskPriority
    {
        get => newTaskPriority;
        set
        {
            newTaskPriority = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddTaskCommand { get; }

    private async Task LoadTasksAsync()
    {
        var tasks = await taskService.GetAllAsync();
        Tasks.Clear();

        foreach (var taskItem in tasks.OrderBy(task => task.DueDate ?? DateTime.MaxValue))
        {
            Tasks.Add(taskItem);
        }

        StatusMessage = $"已从数据库加载 {Tasks.Count} 条任务。";
    }

    private async Task AddTaskAsync()
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
        StatusMessage = "任务已保存到本地数据库。";

        NewTaskTitle = string.Empty;
        NewTaskDueDate = DateTime.Today;
        NewTaskPriority = "中";
    }

    private void AddTask()
    {
        _ = AddTaskAsync();
    }
}
