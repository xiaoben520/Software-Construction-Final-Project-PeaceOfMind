namespace MemoMind.App.Models;

public class UserSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public bool EnableAi { get; set; } = false;
    public bool EnableReminder { get; set; } = true;
    public int ReminderHour { get; set; } = 20;
    public string Theme { get; set; } = "Light";
    public List<string>? SidebarPageIds { get; set; }
    public List<string>? HomePageIds { get; set; }
}
