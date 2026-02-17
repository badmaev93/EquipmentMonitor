using System.Windows;
using EquipmentMonitor.Models;
using EquipmentMonitor.ViewModels;
using EquipmentMonitor.Views;

namespace EquipmentMonitor.Services;

public class DialogService : IDialogService
{
    public Device? ShowAddDeviceDialog()
    {
        var vm = new AddDeviceViewModel();
        var window = new AddDeviceWindow
        {
            DataContext = vm,
            Owner = Application.Current.MainWindow
        };
        var result = window.ShowDialog();
        if (result == true)
        {
            return new Device
            {
                Category = vm.Category,
                Name = vm.Name,
                SerialNumber = vm.SerialNumber,
                InstallDate = vm.InstallDate,
                Status = vm.Status
            };
        }
        return null;
    }

    public bool Confirm(string message, string title)
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }
}
