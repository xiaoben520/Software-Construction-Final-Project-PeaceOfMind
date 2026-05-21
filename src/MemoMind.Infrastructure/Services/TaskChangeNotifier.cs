using MemoMind.Core.Interfaces;

namespace MemoMind.Infrastructure.Services;

public class TaskChangeNotifier : ITaskChangeNotifier
{
    public event Action? TaskChanged;

    public void NotifyTaskChanged()
    {
        TaskChanged?.Invoke();
    }
}
