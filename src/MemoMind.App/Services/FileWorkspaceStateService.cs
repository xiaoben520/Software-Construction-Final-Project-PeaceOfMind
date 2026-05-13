using System.IO;
using System.Text.Json;
using MemoMind.App.Models;

namespace MemoMind.App.Services;

public interface IFileWorkspaceStateService
{
    Task<List<RecentFileEntry>> LoadRecentFilesAsync();
    Task SaveRecentFilesAsync(List<RecentFileEntry> entries);
    Task AddRecentFileAsync(string path);
    Task<List<WorkspaceGroup>> LoadWorkspaceGroupsAsync();
    Task SaveWorkspaceGroupsAsync(List<WorkspaceGroup> groups);
}

public class FileWorkspaceStateService : IFileWorkspaceStateService
{
    private readonly string stateFilePath;
    private readonly JsonSerializerOptions serializerOptions = new() { WriteIndented = true };

    private const int MaxRecentFiles = 50;

    public FileWorkspaceStateService(string stateFilePath)
    {
        this.stateFilePath = stateFilePath;
    }

    public Task<List<RecentFileEntry>> LoadRecentFilesAsync()
    {
        var state = LoadState();
        return Task.FromResult(state?.RecentFiles ?? []);
    }

    public Task SaveRecentFilesAsync(List<RecentFileEntry> entries)
    {
        var state = LoadState() ?? new FileWorkspaceState();
        state.RecentFiles = entries;
        SaveState(state);
        return Task.CompletedTask;
    }

    public async Task AddRecentFileAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalized = path.Trim();
        var isFolder = Directory.Exists(normalized);

        if (!isFolder && !File.Exists(normalized))
        {
            return;
        }

        var files = await LoadRecentFilesAsync();
        var existing = files.FirstOrDefault(x =>
            string.Equals(x.Path, normalized, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            files.Remove(existing);
        }

        files.Insert(0, new RecentFileEntry
        {
            DisplayName = isFolder ? new DirectoryInfo(normalized).Name : Path.GetFileName(normalized),
            Path = normalized,
            IsFolder = isFolder,
            LastOpenedAt = DateTime.Now
        });

        while (files.Count > MaxRecentFiles)
        {
            files.RemoveAt(files.Count - 1);
        }

        await SaveRecentFilesAsync(files);
    }

    public Task<List<WorkspaceGroup>> LoadWorkspaceGroupsAsync()
    {
        var state = LoadState();
        return Task.FromResult(state?.WorkspaceGroups ?? []);
    }

    public Task SaveWorkspaceGroupsAsync(List<WorkspaceGroup> groups)
    {
        var state = LoadState() ?? new FileWorkspaceState();
        state.WorkspaceGroups = groups;
        SaveState(state);
        return Task.CompletedTask;
    }

    private FileWorkspaceState? LoadState()
    {
        if (!File.Exists(stateFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(stateFilePath);
            return JsonSerializer.Deserialize<FileWorkspaceState>(json);
        }
        catch
        {
            return null;
        }
    }

    private void SaveState(FileWorkspaceState state)
    {
        var directory = Path.GetDirectoryName(stateFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, serializerOptions);
        File.WriteAllText(stateFilePath, json);
    }

    private class FileWorkspaceState
    {
        public List<RecentFileEntry> RecentFiles { get; set; } = [];
        public List<WorkspaceGroup> WorkspaceGroups { get; set; } = [];
    }
}
