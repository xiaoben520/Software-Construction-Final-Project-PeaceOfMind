namespace MemoMind.Core.Interfaces;

/// <summary>
/// 任务变更通知器——当 Agent 修改任务后通知 UI 刷新。
/// 单例服务，AgentToolExecutor 发布事件，TaskBoardViewModel 订阅。
/// </summary>
public interface ITaskChangeNotifier
{
    event Action? TaskChanged;
    void NotifyTaskChanged();
}
