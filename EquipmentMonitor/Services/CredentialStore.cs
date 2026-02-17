using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EquipmentMonitor.Services;

public static class CredentialStore
{
    private static readonly string FilePath = Path.Combine(
        AppContext.BaseDirectory, ".saved_login");

    public static void Save(string username, string password)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { u = username, p = password });
        var encrypted = ProtectedData.Protect(payload, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath, encrypted);
    }

    public static (string? Username, string? Password) Load()
    {
        if (!File.Exists(FilePath))
            return (null, null);

        try
        {
            var encrypted = File.ReadAllBytes(FilePath);
            var payload = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            return (root.GetProperty("u").GetString(), root.GetProperty("p").GetString());
        }
        catch
        {
            Delete();
            return (null, null);
        }
    }

    public static void Delete()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }
}
