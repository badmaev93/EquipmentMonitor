using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace EquipmentMonitor.Models;

public enum DeviceCategory
{
    Server,
    Printer,
    PC
}

public enum DeviceStatus
{
    Working,
    Broken,
    Decommissioned
}

public class Device : INotifyPropertyChanged
{
    private DeviceCategory _category;
    private string _name = string.Empty;
    private string _serialNumber = string.Empty;
    private DateTime _installDate = DateTime.Today;
    private DeviceStatus _status;

    public DeviceCategory Category
    {
        get => _category;
        set { if (_category != value) { _category = value; OnPropertyChanged(); OnPropertyChanged(nameof(CategoryDisplayName)); } }
    }

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); } }
    }

    public string SerialNumber
    {
        get => _serialNumber;
        set { if (_serialNumber != value) { _serialNumber = value; OnPropertyChanged(); } }
    }

    public DateTime InstallDate
    {
        get => _installDate;
        set { if (_installDate != value) { _installDate = value; OnPropertyChanged(); } }
    }

    public DeviceStatus Status
    {
        get => _status;
        set { if (_status != value) { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplayName)); } }
    }

    [JsonIgnore]
    public string CategoryDisplayName => Category switch
    {
        DeviceCategory.Server => "Серверы",
        DeviceCategory.Printer => "Принтеры",
        DeviceCategory.PC => "Компьютеры",
        _ => Category.ToString()
    };

    [JsonIgnore]
    public string StatusDisplayName => Status switch
    {
        DeviceStatus.Working => "Работает",
        DeviceStatus.Broken => "Сломано",
        DeviceStatus.Decommissioned => "Списано",
        _ => Status.ToString()
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
