using System.Windows;
using MemoMind.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MemoMind.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MainViewModel>();
    }
}
