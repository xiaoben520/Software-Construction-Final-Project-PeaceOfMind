namespace MemoMind.App.ViewModels;

public interface IPageLifecycleAware
{
    Task OnNavigatedToAsync();
}
