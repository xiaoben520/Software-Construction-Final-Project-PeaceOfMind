namespace MemoMind.App.Models;

public class UserSettings
{
    public string ApiKey { get; set; } = "sk-mnocksjmhuqlbqkshymrtzggvyrzspupfghumzutmvfkpaum";
    public string AiBaseUrl { get; set; } = "https://api.siliconflow.cn/v1";
    public string AiModel { get; set; } = "gpt-3.5-turbo";
    public string AiPersona { get; set; } = "你是一个温和、会倾听、会整理事项的 AI 心灵伙伴。说话简洁、友好、有共情，优先帮用户把事情理清。";
    public bool EnableAi { get; set; } = false;
    public bool EnableReminder { get; set; } = true;
    public int ReminderHour { get; set; } = 20;
    public string Theme { get; set; } = "Light";
    public List<string>? SidebarPageIds { get; set; }
    public List<string>? HomePageIds { get; set; }
}
