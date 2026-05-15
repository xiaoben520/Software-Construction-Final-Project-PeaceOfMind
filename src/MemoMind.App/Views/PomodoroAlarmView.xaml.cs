using System.Windows;
using System.Windows.Controls;
using MemoMind.App.ViewModels;

namespace MemoMind.App.Views;

public partial class PomodoroAlarmView : UserControl
{
    public PomodoroAlarmView()
    {
        InitializeComponent();
    }

    private PomodoroAlarmViewModel? ViewModel => DataContext as PomodoroAlarmViewModel;

    private void WorkMinutesUp_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) ViewModel.WorkMinutes++;
    }

    private void WorkMinutesDown_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) ViewModel.WorkMinutes--;
    }

    private void BreakMinutesUp_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) ViewModel.BreakMinutes++;
    }

    private void BreakMinutesDown_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) ViewModel.BreakMinutes--;
    }

    private void CycleCountUp_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) ViewModel.CycleCount++;
    }

    private void CycleCountDown_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) ViewModel.CycleCount--;
    }
}
