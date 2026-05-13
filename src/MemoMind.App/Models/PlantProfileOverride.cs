namespace MemoMind.App.Models;

public class PlantProfileOverride
{
    public string PlantId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Personality { get; set; }
    public string? SystemPrompt { get; set; }
    public string ImageSourceType { get; set; } = "System"; // System | Local
    public string? ImagePath { get; set; }
    public bool IsDeleted { get; set; }
}
