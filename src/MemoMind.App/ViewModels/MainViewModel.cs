using System.Collections.ObjectModel;
using System.Windows.Input;
using MemoMind.App.Commands;
using MemoMind.App.Models;
using MemoMind.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MemoMind.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IAppSettingsStore settingsStore;
    private NavigationPageItemViewModel? currentPage;

    public MainViewModel()
        : this(App.Services.GetRequiredService<IAppSettingsStore>())
    {
    }

    public MainViewModel(IAppSettingsStore settingsStore)
    {
        this.settingsStore = settingsStore;
        AllPages = [];
        SidebarPages = [];
        HomePages = [];

        NavigateCommand = new RelayCommand(parameter => NavigateTo(parameter?.ToString()));
        InitializePages();
        _ = LoadNavigationLayoutAsync();
    }

    public ObservableCollection<NavigationPageItemViewModel> AllPages { get; }

    public ObservableCollection<NavigationPageItemViewModel> SidebarPages { get; }

    public ObservableCollection<NavigationPageItemViewModel> HomePages { get; }

    public ICommand NavigateCommand { get; }

    public object? CurrentPageViewModel => currentPage?.PageViewModel;

    public string CurrentPageTitle => currentPage?.Title ?? "主页";

    public event Action? NavigationLayoutChanged;

    public void NavigateTo(string? pageId)
    {
        if (string.IsNullOrWhiteSpace(pageId))
        {
            return;
        }

        var targetPage = AllPages.FirstOrDefault(page => string.Equals(page.Id, pageId, StringComparison.OrdinalIgnoreCase));
        if (targetPage is null)
        {
            return;
        }

        currentPage = targetPage;
        OnPropertyChanged(nameof(CurrentPageViewModel));
        OnPropertyChanged(nameof(CurrentPageTitle));

        if (targetPage.PageViewModel is SettingsViewModel settingsViewModel)
        {
            settingsViewModel.SyncFromCurrentLayout();
        }
    }

    public void ApplyLayout(IEnumerable<string> sidebarPageIds, IEnumerable<string> homePageIds)
    {
        var sidebarSet = sidebarPageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var homeSet = homePageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        ApplyVisibility(sidebarSet, homeSet);
    }

    public async Task ApplySettingsAsync(UserSettings settings)
    {
        var sidebarPageIds = settings.SidebarPageIds ?? [];
        var homePageIds = settings.HomePageIds ?? [];
        ApplyLayout(sidebarPageIds, homePageIds);
        settings.SidebarPageIds = SidebarPages.Select(page => page.Id).ToList();
        settings.HomePageIds = HomePages.Select(page => page.Id).ToList();
        await settingsStore.SaveAsync(settings);
    }

    private void InitializePages()
    {
        foreach (var definition in AppPageCatalog.All)
        {
            var pageViewModel = App.Services.GetRequiredService(definition.ViewModelType);
            AllPages.Add(new NavigationPageItemViewModel(definition, pageViewModel));
        }

        var homeViewModel = AllPages.FirstOrDefault(page => page.PageViewModel is HomeViewModel)?.PageViewModel as HomeViewModel;
        homeViewModel?.Configure(this);

        var settingsViewModel = AllPages.FirstOrDefault(page => page.PageViewModel is SettingsViewModel)?.PageViewModel as SettingsViewModel;
        settingsViewModel?.Configure(this);

        RefreshVisiblePages();
        NavigateTo("home");
    }

    private async Task LoadNavigationLayoutAsync()
    {
        var settings = await settingsStore.LoadAsync();
        var sidebarSet = BuildInitialSelectionSet(settings.SidebarPageIds, page => page.Definition.DefaultInSidebar);
        var homeSet = BuildInitialSelectionSet(settings.HomePageIds, page => page.Definition.DefaultOnHome);

        ApplyVisibility(sidebarSet, homeSet);
    }

    private HashSet<string> BuildInitialSelectionSet(IEnumerable<string>? configuredIds, Func<NavigationPageItemViewModel, bool> defaultSelector)
    {
        if (configuredIds is not null)
        {
            return configuredIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return AllPages
            .Where(defaultSelector)
            .Select(page => page.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private void ApplyVisibility(HashSet<string> sidebarSet, HashSet<string> homeSet)
    {
        foreach (var page in AllPages)
        {
            page.ShowInSidebar = page.SidebarLocked || sidebarSet.Contains(page.Id);
            page.ShowOnHome = homeSet.Contains(page.Id);
        }

        RefreshVisiblePages();

        if (currentPage is null || !SidebarPages.Contains(currentPage))
        {
            NavigateTo("home");
        }

        NavigationLayoutChanged?.Invoke();
    }

    private void RefreshVisiblePages()
    {
        SidebarPages.Clear();
        foreach (var page in AllPages.Where(page => page.ShowInSidebar))
        {
            SidebarPages.Add(page);
        }

        HomePages.Clear();
        foreach (var page in AllPages.Where(page => page.ShowOnHome))
        {
            HomePages.Add(page);
        }
    }
}
