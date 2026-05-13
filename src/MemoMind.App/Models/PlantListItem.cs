namespace MemoMind.App.Models;

public class PlantListItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Emoji { get; set; } = "🌱";
    public string Personality { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
    public int? CustomId { get; set; }
    public bool IsDeleted { get; set; }
}
