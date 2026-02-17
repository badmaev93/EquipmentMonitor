using System.Data;
using EquipmentMonitor.Models;
using Microsoft.Data.Sqlite;

namespace EquipmentMonitor.Services;

public static class DatabaseService
{
    private const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS Devices (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Category TEXT NOT NULL,
            Name TEXT NOT NULL,
            SerialNumber TEXT NOT NULL,
            InstallDate TEXT NOT NULL,
            Status TEXT NOT NULL
        )
        """;

    public static List<Device> Import(string connectionString)
    {
        var devices = new List<Device>();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Devices'";
        if (checkCmd.ExecuteScalar() is null)
            return devices;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Category, Name, SerialNumber, InstallDate, Status FROM Devices";
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            Enum.TryParse<DeviceCategory>(reader.GetString(0), true, out var category);
            var name = reader.GetString(1);
            var serial = reader.GetString(2);
            DateTime.TryParse(reader.GetString(3), out var date);
            Enum.TryParse<DeviceStatus>(reader.GetString(4), true, out var status);

            devices.Add(new Device
            {
                Category = category,
                Name = name,
                SerialNumber = serial,
                InstallDate = date == default ? DateTime.Today : date,
                Status = status
            });
        }
        return devices;
    }

    public static void Export(string connectionString, List<Device> devices)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var createCmd = connection.CreateCommand();
        createCmd.CommandText = CreateTableSql;
        createCmd.ExecuteNonQuery();

        using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM Devices";
        deleteCmd.ExecuteNonQuery();

        foreach (var device in devices)
        {
            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = """
                INSERT INTO Devices (Category, Name, SerialNumber, InstallDate, Status)
                VALUES ($category, $name, $serial, $date, $status)
                """;
            insertCmd.Parameters.AddWithValue("$category", device.Category.ToString());
            insertCmd.Parameters.AddWithValue("$name", device.Name);
            insertCmd.Parameters.AddWithValue("$serial", device.SerialNumber);
            insertCmd.Parameters.AddWithValue("$date", device.InstallDate.ToString("yyyy-MM-dd"));
            insertCmd.Parameters.AddWithValue("$status", device.Status.ToString());
            insertCmd.ExecuteNonQuery();
        }
    }
}
