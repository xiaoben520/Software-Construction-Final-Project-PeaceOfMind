using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MemoMind.App.ViewModels;

namespace MemoMind.App.Views;

public partial class CyberPlantView : UserControl
{
    public CyberPlantView()
    {
        InitializeComponent();
    }

    private void PlantType_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Models.CyberPlantType plantType)
        {
            if (DataContext is CyberPlantViewModel vm)
            {
                vm.SelectPlantCommand.Execute(plantType.Id);
            }
        }
    }

    private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is CyberPlantViewModel vm)
        {
            vm.SendCommand.Execute(null);
        }
    }
}
