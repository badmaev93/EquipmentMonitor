using EquipmentMonitor.Models;

namespace EquipmentMonitor.Services;

public interface IDialogService
{
    Device? ShowAddDeviceDialog();
    bool Confirm(string message, string title);
}
