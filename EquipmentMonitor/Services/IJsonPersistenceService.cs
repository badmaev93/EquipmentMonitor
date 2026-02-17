using EquipmentMonitor.Models;

namespace EquipmentMonitor.Services;

public interface IJsonPersistenceService
{
    List<Device> Load();
    void Save(IEnumerable<Device> devices);
    ViewSettings LoadSettings();
    void SaveSettings(ViewSettings settings);
}
