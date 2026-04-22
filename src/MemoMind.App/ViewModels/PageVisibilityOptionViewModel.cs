namespace MemoMind.App.ViewModels;

public class PageVisibilityOptionViewModel : ViewModelBase
{
    private bool isSelected;

    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public bool IsLocked { get; init; }

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (IsLocked)
            {
                isSelected = true;
                return;
            }

            if (isSelected == value)
            {
                return;
            }

            isSelected = value;
            OnPropertyChanged();
        }
    }
}
