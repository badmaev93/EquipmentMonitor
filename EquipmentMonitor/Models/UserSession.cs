namespace EquipmentMonitor.Models;

public class UserSession
{
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public DateTime LoginTime { get; set; } = DateTime.Now;
}
