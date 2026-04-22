using System.Collections.ObjectModel;
using System.Windows.Input;
using MemoMind.App.Commands;

namespace MemoMind.App.ViewModels;

public class HomeViewModel : ViewModelBase
{
    private MainViewModel? mainViewModel;

    public HomeViewModel()
    {
        HomeModules = [];
        OpenModuleCommand = new RelayCommand(parameter =>
        {
            if (parameter is not string pageId)
            {
                return;
            }

            mainViewModel?.NavigateTo(pageId);
        });
    }

    public string WelcomeTitle => "欢迎使用心安·MemoMind";

    public string WelcomeSubtitle => "可在设置中自由调整左侧栏目与主页展示模块。";

    public ObservableCollection<NavigationPageItemViewModel> HomeModules { get; }

    public ICommand OpenModuleCommand { get; }

    public void Configure(MainViewModel mainViewModel)
    {
        if (this.mainViewModel is not null)
        {
            this.mainViewModel.NavigationLayoutChanged -= HandleNavigationLayoutChanged;
        }

        this.mainViewModel = mainViewModel;
        this.mainViewModel.NavigationLayoutChanged += HandleNavigationLayoutChanged;
        SyncHomeModules();
    }

    private void HandleNavigationLayoutChanged()
    {
        SyncHomeModules();
    }

    private void SyncHomeModules()
    {
        if (mainViewModel is null)
        {
            return;
        }

        HomeModules.Clear();
        foreach (var page in mainViewModel.HomePages)
        {
            HomeModules.Add(page);
        }
    }
}
