namespace MemoMind.App.Models;

public class WorkspaceGroup
{
    public string Name { get; set; } = string.Empty;
    public List<string> RootPaths { get; set; } = [];
}
