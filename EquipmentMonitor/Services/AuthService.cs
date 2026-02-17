using System.Diagnostics;
using EquipmentMonitor.Models;
using Npgsql;

namespace EquipmentMonitor.Services;

public class AuthService
{
    private readonly string _host;
    private readonly int _port;

    public AuthService(string host = "localhost", int port = 5432)
    {
        _host = host;
        _port = port;
    }

    private static NpgsqlDataSource CreateDataSource(string host, int port, string username, string password, int timeout = 30)
    {
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = "neondb",
            Username = username,
            Password = password,
            SslMode = SslMode.Require,
            Timeout = timeout,
            KeepAlive = 30,
            CommandTimeout = 60
        };

        var dsb = new NpgsqlDataSourceBuilder(csb.ConnectionString);
        return dsb.Build();
    }

    public static string BuildSessionConnectionString(string host, int port, string username, string password)
    {
        return new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = "neondb",
            Username = username,
            Password = password,
            SslMode = SslMode.Require,
            Timeout = 30,
            KeepAlive = 30,
            CommandTimeout = 60
        }.ConnectionString;
    }

    public (bool Success, string? Error) ValidateCredentials(string username, string password, string host, int port)
    {
        try
        {
            using var ds = CreateDataSource(host, port, username, password, 45);
            using var conn = ds.OpenConnection();
            using var cmd = new NpgsqlCommand("SELECT 1", conn);
            cmd.ExecuteScalar();
            return (true, null);
        }
        catch (NpgsqlException ex) when (ex.Message.Contains("password") || ex.Message.Contains("authentication"))
        {
            return (false, "Неверный логин или пароль");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Auth] {ex.GetType().Name}: {ex.Message}");
            return (false, $"Ошибка подключения: {ex.Message}");
        }
    }

    public UserSession CreateSession(string username, string password, string host, int port)
    {
        var cs = BuildSessionConnectionString(host, port, username, password);
        try
        {
            using var ds = CreateDataSource(host, port, username, password);
            using var conn = ds.OpenConnection();

            using var cmd = new NpgsqlCommand(@"
                SELECT
                    (e.last_name || ' ' || e.first_name || ' ' || COALESCE(e.middle_name, ''))::TEXT,
                    d.name::TEXT,
                    CASE
                        WHEN @u LIKE '%admin%' THEN 'Администратор'
                        WHEN @u LIKE '%head%' THEN 'Руководитель'
                        WHEN @u LIKE '%analyst%' THEN 'Аналитик'
                        WHEN @u LIKE '%auditor%' THEN 'Аудитор'
                        WHEN @u LIKE '%etl%' THEN 'ETL-сервис'
                        ELSE 'Оператор'
                    END::TEXT
                FROM ods.employees e
                JOIN ods.departments d ON e.department_id = d.department_id
                WHERE e.db_username = @u AND e.is_active
                LIMIT 1", conn);
            cmd.Parameters.AddWithValue("u", username);
            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return new UserSession
                {
                    Username = username,
                    FullName = reader.IsDBNull(0) ? username : reader.GetString(0),
                    Department = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Role = reader.IsDBNull(2) ? "Оператор" : reader.GetString(2),
                    ConnectionString = cs
                };
            }

            return new UserSession
            {
                Username = username,
                FullName = username,
                Department = "",
                Role = "Пользователь",
                ConnectionString = cs
            };
        }
        catch
        {
            return new UserSession { Username = username, ConnectionString = cs };
        }
    }
}
