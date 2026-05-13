namespace MemoMind.Core.Models;

public class CustomPlantProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = "我的植物";
    public string Personality { get; set; } = "温柔";
    public string SystemPrompt { get; set; } = "你是一株温柔的植物伙伴，介绍自己的习性和照料方式，偶尔借植物谈谈为人处世。回复简短，语气亲切。";
    public string ImageSourceType { get; set; } = "System"; // System | Local
    public string ImagePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
