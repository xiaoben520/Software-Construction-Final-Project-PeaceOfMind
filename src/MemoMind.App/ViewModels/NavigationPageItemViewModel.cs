using MemoMind.App.Models;

namespace MemoMind.App.ViewModels;

public class NavigationPageItemViewModel : ViewModelBase
{
    private bool showInSidebar;
    private bool showOnHome;

    public NavigationPageItemViewModel(AppPageDefinition definition, object pageViewModel)
    {
        Definition = definition;
        PageViewModel = pageViewModel;
        showInSidebar = definition.DefaultInSidebar;
        showOnHome = definition.DefaultOnHome;
    }

    public AppPageDefinition Definition { get; }

    public string Id => Definition.Id;

    public string Title => Definition.Title;

    public string Description => Definition.Description;

    public bool SidebarLocked => Definition.SidebarLocked;

    public object PageViewModel { get; }

    public bool ShowInSidebar
    {
        get => showInSidebar;
        set
        {
            if (SidebarLocked)
            {
                showInSidebar = true;
                return;
            }

            if (showInSidebar == value)
            {
                return;
            }

            showInSidebar = value;
            OnPropertyChanged();
        }
    }

    public bool ShowOnHome
    {
        get => showOnHome;
        set
        {
            if (showOnHome == value)
            {
                return;
            }

            showOnHome = value;
            OnPropertyChanged();
        }
    }
}
