using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MemoMind.App.Views;

public partial class TaskBoardView : UserControl
{
    public TaskBoardView()
    {
        InitializeComponent();
    }

    private void OnPanelPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Clicking blank space or non-interactive area in the panel
        // clears TextBox focus so the watermark reappears
        var dep = e.OriginalSource as DependencyObject;
        while (dep is not null && dep != ControlPanelBorder)
        {
            if (dep is UIElement ui && ui.Focusable)
                return;
            dep = dep is Visual
                ? VisualTreeHelper.GetParent(dep)
                : LogicalTreeHelper.GetParent(dep);
        }
        Keyboard.ClearFocus();
    }
}
