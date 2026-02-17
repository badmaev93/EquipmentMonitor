using System.Collections.ObjectModel;
using EquipmentMonitor.Models;
using Npgsql;

namespace EquipmentMonitor.Services;

public class PostgresDataService
{
    private readonly string _connectionString;

    public PostgresDataService(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// PULL: Загрузка данных из PostgreSQL (ODS) в локальную коллекцию
    /// </summary>
    public List<Device> Pull()
    {
        var devices = new List<Device>();
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand(@"
            SELECT d.serial_number, d.name, c.code AS category, s.code AS status, d.install_date
            FROM ods.devices d
            JOIN ods.device_categories c ON d.category_id = c.category_id
            JOIN ods.device_statuses s ON d.status_id = s.status_id
            WHERE NOT d.is_deleted
            ORDER BY d.name", conn);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var catCode = reader.GetString(2);
            var statusCode = reader.GetString(3);

            devices.Add(new Device
            {
                SerialNumber = reader.GetString(0),
                Name = reader.GetString(1),
                Category = MapCategory(catCode),
                Status = MapStatus(statusCode),
                InstallDate = reader.GetDateTime(4)
            });
        }
        return devices;
    }

    /// <summary>
    /// COMMIT: Сохранение локальных изменений в staging (через ETL pipeline)
    /// </summary>
    public (int Inserted, int Updated, int Rejected) Commit(ObservableCollection<Device> devices)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        var jsonArray = System.Text.Json.JsonSerializer.Serialize(
            devices.Select(d => new
            {
                d.Name,
                d.SerialNumber,
                Category = d.Category.ToString().ToUpper(),
                Status = d.Status switch
                {
                    DeviceStatus.Working => "WORKING",
                    DeviceStatus.Broken => "BROKEN",
                    DeviceStatus.Decommissioned => "DECOMMISSIONED",
                    _ => "WORKING"
                },
                InstallDate = d.InstallDate.ToString("yyyy-MM-dd")
            }).ToList(),
            new System.Text.Json.JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

        // 1. Load into staging
        using var loadCmd = new NpgsqlCommand("SELECT staging.load_raw_devices(@source, @data::jsonb)", conn);
        loadCmd.Parameters.AddWithValue("source", "APP");
        loadCmd.Parameters.AddWithValue("data", jsonArray);
        var batchId = (Guid)loadCmd.ExecuteScalar()!;

        // 2. Transform staging -> ODS
        using var transformCmd = new NpgsqlCommand("SELECT * FROM ods.transform_from_staging(@batch::uuid)", conn);
        transformCmd.Parameters.AddWithValue("batch", batchId);
        using var reader = transformCmd.ExecuteReader();

        int inserted = 0, updated = 0, rejected = 0;
        if (reader.Read())
        {
            inserted = reader.GetInt32(0);
            updated = reader.GetInt32(1);
            rejected = reader.GetInt32(2);
        }

        return (inserted, updated, rejected);
    }

    /// <summary>
    /// PUSH: Полный ETL-цикл — обновляет DWH, витрины, проверяет DQ
    /// </summary>
    public List<(string Step, string Status, string Details)> Push()
    {
        var results = new List<(string, string, string)>();
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand("SELECT * FROM meta.run_full_etl()", conn);
        cmd.CommandTimeout = 120;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? "" : reader.GetString(2)
            ));
        }
        return results;
    }

    public bool TestConnection()
    {
        try
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static DeviceCategory MapCategory(string code) => code.ToUpper() switch
    {
        "SERVER" or "SRV-DB" or "SRV-APP" or "SRV-FILE" or "SRV-BACKUP" => DeviceCategory.Server,
        "PRINTER" or "PRN-LASER" or "PRN-MFU" or "PRN-INKJET" => DeviceCategory.Printer,
        _ => DeviceCategory.PC
    };

    private static DeviceStatus MapStatus(string code) => code.ToUpper() switch
    {
        "WORKING" or "NEW" or "INSTALLING" or "RESERVED" or "MAINTENANCE" => DeviceStatus.Working,
        "BROKEN" or "REPAIR" => DeviceStatus.Broken,
        "DECOMMISSIONED" or "DISPOSED" => DeviceStatus.Decommissioned,
        _ => DeviceStatus.Working
    };
}
