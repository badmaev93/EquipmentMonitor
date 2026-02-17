using System.Windows;
using System.Windows.Input;
using EquipmentMonitor.Models;
using EquipmentMonitor.ViewModels.Base;

namespace EquipmentMonitor.ViewModels;

public class AddDeviceViewModel : ViewModelBase
{
    private DeviceCategory _category;
    private string _name = string.Empty;
    private string _serialNumber = string.Empty;
    private DateTime _installDate = DateTime.Today;
    private DeviceStatus _status = DeviceStatus.Working;

    public DeviceCategory Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string SerialNumber
    {
        get => _serialNumber;
        set => SetProperty(ref _serialNumber, value);
    }

    public DateTime InstallDate
    {
        get => _installDate;
        set => SetProperty(ref _installDate, value);
    }

    public DeviceStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public Array CategoryValues => Enum.GetValues(typeof(DeviceCategory));
    public Array StatusValues => Enum.GetValues(typeof(DeviceStatus));

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public AddDeviceViewModel()
    {
        ConfirmCommand = new RelayCommand(
            o => Confirm(o as Window),
            o => !string.IsNullOrWhiteSpace(Name));
        CancelCommand = new RelayCommand(o => Cancel(o as Window));
    }

    private void Confirm(Window? window)
    {
        if (window is null) return;
        window.DialogResult = true;
        window.Close();
    }

    private void Cancel(Window? window)
    {
        if (window is null) return;
        window.DialogResult = false;
        window.Close();
    }
}
