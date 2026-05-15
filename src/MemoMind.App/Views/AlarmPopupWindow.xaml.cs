using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace MemoMind.App.Views;

public partial class AlarmPopupWindow : Window
{
    public AlarmPopupWindow(string title, string message, DateTime triggerTime)
    {
        InitializeComponent();
        PopupTitle = title;
        Message = message;
        TriggerTime = triggerTime;
        DataContext = this;
    }

    public string PopupTitle { get; }
    public string Message { get; }
    public DateTime TriggerTime { get; }

    private void DismissButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
