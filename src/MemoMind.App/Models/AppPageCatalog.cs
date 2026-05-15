using MemoMind.App.ViewModels;

namespace MemoMind.App.Models;

public static class AppPageCatalog
{
    public static IReadOnlyList<AppPageDefinition> All { get; } =
    [
        new AppPageDefinition
        {
            Id = "home",
            Title = "主页",
            Description = "查看今日概览与快捷入口。",
            ViewModelType = typeof(HomeViewModel),
            DefaultInSidebar = true,
            DefaultOnHome = false,
            SidebarLocked = true
        },
        new AppPageDefinition
        {
            Id = "task-board",
            Title = "任务看板",
            Description = "管理任务、优先级与到期时间。",
            ViewModelType = typeof(TaskBoardViewModel),
            DefaultInSidebar = true,
            DefaultOnHome = true
        },
        new AppPageDefinition
        {
            Id = "chat",
            Title = "AI 聊天",
            Description = "进行对话并提取可执行事项。",
            ViewModelType = typeof(ChatViewModel),
            DefaultInSidebar = true,
            DefaultOnHome = true
        },
        new AppPageDefinition
        {
            Id = "workspace",
            Title = "文件工作区",
            Description = "管理常用文件夹与项目路径。",
            ViewModelType = typeof(FileWorkspaceViewModel),
            DefaultInSidebar = true,
            DefaultOnHome = true
        },
        new AppPageDefinition
        {
            Id = "cyber-plant",
            Title = "赛博植物",
            Description = "领养一株虚拟植物，和它聊天、给它浇水。",
            ViewModelType = typeof(CyberPlantViewModel),
            DefaultInSidebar = true,
            DefaultOnHome = true
        },
        new AppPageDefinition
        {
            Id = "pomodoro-alarm",
            Title = "专注 & 闹钟",
            Description = "番茄钟计时、定时闹钟与倒计时。",
            ViewModelType = typeof(PomodoroAlarmViewModel),
            DefaultInSidebar = true,
            DefaultOnHome = true
        },
        new AppPageDefinition
        {
            Id = "settings",
            Title = "设置",
            Description = "调整系统配置与页面可见性。",
            ViewModelType = typeof(SettingsViewModel),
            DefaultInSidebar = true,
            DefaultOnHome = true,
            SidebarLocked = true
        }
    ];
}
