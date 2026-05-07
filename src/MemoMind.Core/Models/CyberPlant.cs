namespace MemoMind.Core.Models;

public class CyberPlant
{
    public string PlantType { get; set; } = "cactus";
    public string PlantName { get; set; } = "小仙人掌";
    public int GrowthLevel { get; set; } = 1;
    public string Mood { get; set; } = "开心";
    public DateTime LastWateredAt { get; set; } = DateTime.Now;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<PlantMessage> Messages { get; set; } = [];
}

public class PlantMessage
{
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Time { get; set; } = DateTime.Now;
}
