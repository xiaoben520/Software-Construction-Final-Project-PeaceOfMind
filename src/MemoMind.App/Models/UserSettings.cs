namespace MemoMind.App.Models;

public class UserSettings
{
    public string ApiKey { get; set; } = "sk-mnocksjmhuqlbqkshymrtzggvyrzspupfghumzutmvfkpaum";
    public string AiBaseUrl { get; set; } = "https://api.siliconflow.cn/v1";
    public string AiModel { get; set; } = "deepseek-v4-flash";
    public string AiPersona { get; set; } = "你是一个温和、会倾听、会整理事项的 AI 心灵伙伴。说话简洁、友好、有共情，优先帮用户把事情理清。";
    public bool EnableAi { get; set; } = false;
    public bool EnableReminder { get; set; } = true;
    public int ReminderHour { get; set; } = 20;
    public string Theme { get; set; } = "System";
    public bool ShowRecentFiles { get; set; } = true;
    public bool ShowWorkspaceGroups { get; set; } = true;
    public bool ShowFileManager { get; set; } = true;
    public int RecentFilesLimit { get; set; } = 50;
    public string FileManagerRootPath { get; set; } = string.Empty;
    public List<string> FileManagerRootPaths { get; set; } = [];
    public List<string> FileManagerExpandedPaths { get; set; } = [];
    public List<string> FileManagerHiddenPaths { get; set; } = [];
    public List<string>? SidebarPageIds { get; set; }
    public List<string>? HomePageIds { get; set; }

    // Sound & popup settings for timer/alarm module
    public bool PomodoroSoundEnabled { get; set; } = true;
    public bool AlarmSoundEnabled { get; set; } = true;
    public bool CountdownSoundEnabled { get; set; } = true;
    public bool PomodoroPopupEnabled { get; set; } = true;
    public bool AlarmPopupEnabled { get; set; } = true;
    public bool CountdownPopupEnabled { get; set; } = true;
    public bool UseCustomSound { get; set; } = false;
    public string CustomSoundPath { get; set; } = string.Empty;
}
