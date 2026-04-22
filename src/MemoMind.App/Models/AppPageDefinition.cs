namespace MemoMind.App.Models;

public sealed class AppPageDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required Type ViewModelType { get; init; }
    public bool DefaultInSidebar { get; init; } = true;
    public bool DefaultOnHome { get; init; } = true;
    public bool SidebarLocked { get; init; } = false;
}
