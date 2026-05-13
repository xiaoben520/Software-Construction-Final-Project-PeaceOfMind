namespace MemoMind.Core.Models;

public class CyberPlant
{
    public string PlantType { get; set; } = "cactus";
    public string PlantName { get; set; } = "小仙人掌";
    public string CustomSpecies { get; set; } = "";
    public string CustomEmoji { get; set; } = "";
    public string CustomSystemPrompt { get; set; } = "";
    public string CustomImagePath { get; set; } = "";
    public int GrowthLevel { get; set; } = 1;
    public string Mood { get; set; } = "开心";
    public DateTime LastWateredAt { get; set; } = DateTime.Now;
    public DateTime LastFertilizedAt { get; set; } = DateTime.Now;
    public DateTime LastSunbathedAt { get; set; } = DateTime.Now;
    public DateTime LastCareDecayAt { get; set; } = DateTime.Now;
    public DateTime LastChatClearedAt { get; set; } = DateTime.Today;
    public int WaterValue { get; set; } = 6;
    public int NutritionValue { get; set; } = 6;
    public int SunValue { get; set; } = 6;
    public int MaxWater { get; set; } = 12;
    public int MaxNutrition { get; set; } = 12;
    public int MaxSun { get; set; } = 12;
    public int NeedWater { get; set; } = 6;
    public int NeedNutrition { get; set; } = 6;
    public int NeedSun { get; set; } = 6;
    public bool IsCareLocked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<PlantMessage> Messages { get; set; } = [];
    public Dictionary<string, PlantCareState> PlantStates { get; set; } = new();
}

public class PlantCareState
{
    public string PlantType { get; set; } = string.Empty;
    public string PlantName { get; set; } = string.Empty;
    public string CustomEmoji { get; set; } = string.Empty;
    public string CustomSystemPrompt { get; set; } = string.Empty;
    public string CustomImagePath { get; set; } = string.Empty;
    public int GrowthLevel { get; set; } = 1;
    public string Mood { get; set; } = "开心";
    public DateTime LastWateredAt { get; set; } = DateTime.Now;
    public DateTime LastFertilizedAt { get; set; } = DateTime.Now;
    public DateTime LastSunbathedAt { get; set; } = DateTime.Now;
    public DateTime LastCareDecayAt { get; set; } = DateTime.Now;
    public DateTime LastChatClearedAt { get; set; } = DateTime.Today;
    public int WaterValue { get; set; } = 6;
    public int NutritionValue { get; set; } = 6;
    public int SunValue { get; set; } = 6;
    public int MaxWater { get; set; } = 12;
    public int MaxNutrition { get; set; } = 12;
    public int MaxSun { get; set; } = 12;
    public int NeedWater { get; set; } = 6;
    public int NeedNutrition { get; set; } = 6;
    public int NeedSun { get; set; } = 6;
    public bool IsCareLocked { get; set; }
    public List<PlantMessage> Messages { get; set; } = [];
}

public class PlantMessage
{
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Time { get; set; } = DateTime.Now;
}
