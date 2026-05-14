using System.IO;
using System.Data;
using System.Windows;
using MemoMind.App.Services;
using MemoMind.App.ViewModels;
using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;
using MemoMind.Infrastructure.Data;
using MemoMind.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace MemoMind.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = default!;
    private static readonly string[] ThemeResourceFiles =
    [
        "Resources/Themes/Theme.Light.xaml",
        "Resources/Themes/Theme.Dark.xaml",
        "Resources/Themes/Theme.Forest.xaml",
        "Resources/Themes/Theme.Ocean.xaml",
        "Resources/Themes/Theme.Sunset.xaml"
    ];

    public static readonly IReadOnlyList<string> AllThemes = ["System", "Light", "Dark", "Forest", "Ocean", "Sunset"];

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MemoMind");
        Directory.CreateDirectory(appDataFolder);

        var databasePath = Path.Combine(appDataFolder, "MemoMind.db");
        var settingsFilePath = Path.Combine(appDataFolder, "settings.json");
        var fileWorkspaceStatePath = Path.Combine(appDataFolder, "file_workspace_state.json");
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IFileWorkspaceService, FileWorkspaceService>();
        services.AddScoped<ICustomPlantService, CustomPlantService>();
        services.AddSingleton<IAppSettingsStore>(_ => new JsonAppSettingsStore(settingsFilePath));
        services.AddSingleton<IFileWorkspaceStateService>(_ => new FileWorkspaceStateService(fileWorkspaceStatePath));
        services.AddSingleton<IChatService, ChatService>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<TaskBoardViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<FileWorkspaceViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<CyberPlantViewModel>();
        Services = services.BuildServiceProvider();

        var settingsStore = Services.GetRequiredService<IAppSettingsStore>();
        var userSettings = settingsStore.LoadAsync().GetAwaiter().GetResult();
        ApplyTheme(userSettings.Theme);

        SystemEvents.UserPreferenceChanged += (_, _) =>
        {
            var currentSettings = settingsStore.LoadAsync().GetAwaiter().GetResult();
            if (string.Equals(currentSettings.Theme, "System", StringComparison.OrdinalIgnoreCase))
            {
                Current.Dispatcher.Invoke(() => ApplyTheme("System"));
            }
        };

        using (var scope = Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                context.Database.Migrate();
            }
            catch
            {
                context.Database.EnsureCreated();
            }

            if (!HasTable(context, "Tasks"))
            {
                context.Database.EnsureDeleted();
                context.Database.Migrate();
            }

            if (!context.Tasks.Any())
            {
                context.Tasks.AddRange(
                    new TaskItem
                    {
                        Title = "计网作业",
                        Description = "完成课程作业并整理提交材料",
                        DueDate = DateTime.Today.AddDays(2),
                        IsUrgent = true,
                        Status = "Todo",
                        SourceType = "Seed"
                    },
                    new TaskItem
                    {
                        Title = "小组讨论",
                        Description = "准备项目分工与展示内容",
                        DueDate = DateTime.Today.AddDays(1),
                        IsUrgent = false,
                        Status = "Doing",
                        SourceType = "Seed"
                    });

                context.SaveChanges();
            }

            EnsureCustomPlantsTable(context);
        }

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    public static void ApplyTheme(string? theme)
    {
        var normalizedTheme = NormalizeTheme(theme);
        var targetSource = $"Resources/Themes/Theme.{normalizedTheme}.xaml";

        var appResources = Current.Resources;
        var mergedDictionaries = appResources.MergedDictionaries;

        for (var i = mergedDictionaries.Count - 1; i >= 0; i--)
        {
            var source = mergedDictionaries[i].Source?.OriginalString;
            if (source is not null && ThemeResourceFiles.Any(x => string.Equals(x, source, StringComparison.OrdinalIgnoreCase)))
            {
                mergedDictionaries.RemoveAt(i);
            }
        }

        mergedDictionaries.Insert(0, new ResourceDictionary { Source = new Uri(targetSource, UriKind.Relative) });
    }

    public static string NormalizeTheme(string? theme)
    {
        if (string.Equals(theme, "System", StringComparison.OrdinalIgnoreCase))
        {
            return IsSystemDarkMode() ? "Dark" : "Light";
        }

        if (string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase)) return "Dark";
        if (string.Equals(theme, "Forest", StringComparison.OrdinalIgnoreCase)) return "Forest";
        if (string.Equals(theme, "Ocean", StringComparison.OrdinalIgnoreCase)) return "Ocean";
        if (string.Equals(theme, "Sunset", StringComparison.OrdinalIgnoreCase)) return "Sunset";
        return "Light";
    }

    private static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int intValue && intValue == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasTable(AppDbContext context, string tableName)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = Convert.ToInt32(command.ExecuteScalar());
        return result > 0;
    }

    private static void EnsureCustomPlantsTable(AppDbContext context)
    {
        if (HasTable(context, "CustomPlantProfiles"))
        {
            return;
        }

        context.Database.ExecuteSqlRaw(
            "CREATE TABLE IF NOT EXISTS CustomPlantProfiles (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "Name TEXT NOT NULL, " +
            "Personality TEXT NOT NULL, " +
            "SystemPrompt TEXT NOT NULL, " +
            "ImageSourceType TEXT NOT NULL, " +
            "ImagePath TEXT NOT NULL, " +
            "CreatedAt TEXT NOT NULL, " +
            "UpdatedAt TEXT NOT NULL" +
            ");");
    }
}
