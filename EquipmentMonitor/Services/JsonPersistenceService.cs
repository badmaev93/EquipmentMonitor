using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using EquipmentMonitor.Models;

namespace EquipmentMonitor.Services;

public class JsonPersistenceService : IJsonPersistenceService
{
    private readonly string _filePath;
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _options;

    public JsonPersistenceService()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        _filePath = Path.Combine(dir, "devices.json");
        _settingsPath = Path.Combine(dir, "settings.json");
        _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public List<Device> Load()
    {
        if (!File.Exists(_filePath))
            return [];

        var json = File.ReadAllText(_filePath, Encoding.UTF8);
        return JsonSerializer.Deserialize<List<Device>>(json, _options) ?? [];
    }

    public void Save(IEnumerable<Device> devices)
    {
        var json = JsonSerializer.Serialize(devices.ToList(), _options);
        File.WriteAllText(_filePath, json, new UTF8Encoding(true));
    }

    public ViewSettings LoadSettings()
    {
        if (!File.Exists(_settingsPath))
            return new ViewSettings();

        try
        {
            var json = File.ReadAllText(_settingsPath, Encoding.UTF8);
            return JsonSerializer.Deserialize<ViewSettings>(json, _options) ?? new ViewSettings();
        }
        catch
        {
            return new ViewSettings();
        }
    }

    public void SaveSettings(ViewSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _options);
        File.WriteAllText(_settingsPath, json, new UTF8Encoding(true));
    }
}
