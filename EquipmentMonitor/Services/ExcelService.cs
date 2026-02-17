using System.IO;
using EquipmentMonitor.Models;
using OfficeOpenXml;

namespace EquipmentMonitor.Services;

public static class ExcelService
{
    static ExcelService()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public static List<Device> ImportFromXlsx(string filePath)
    {
        var devices = new List<Device>();
        using var package = new ExcelPackage(new FileInfo(filePath));
        var ws = package.Workbook.Worksheets[0];
        if (ws is null) return devices;

        for (int row = 2; row <= ws.Dimension.End.Row; row++)
        {
            var categoryStr = ws.Cells[row, 1].Text.Trim();
            var name = ws.Cells[row, 2].Text.Trim();
            var serial = ws.Cells[row, 3].Text.Trim();
            var dateStr = ws.Cells[row, 4].Text.Trim();
            var statusStr = ws.Cells[row, 5].Text.Trim();

            if (string.IsNullOrWhiteSpace(name)) continue;

            Enum.TryParse<DeviceCategory>(categoryStr, true, out var category);
            Enum.TryParse<DeviceStatus>(statusStr, true, out var status);
            DateTime.TryParse(dateStr, out var date);

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

    public static void ExportToXlsx(string filePath, List<Device> devices)
    {
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Устройства");

        ws.Cells[1, 1].Value = "Category";
        ws.Cells[1, 2].Value = "Name";
        ws.Cells[1, 3].Value = "SerialNumber";
        ws.Cells[1, 4].Value = "InstallDate";
        ws.Cells[1, 5].Value = "Status";

        using var range = ws.Cells[1, 1, 1, 5];
        range.Style.Font.Bold = true;

        for (int i = 0; i < devices.Count; i++)
        {
            var d = devices[i];
            ws.Cells[i + 2, 1].Value = d.Category.ToString();
            ws.Cells[i + 2, 2].Value = d.Name;
            ws.Cells[i + 2, 3].Value = d.SerialNumber;
            ws.Cells[i + 2, 4].Value = d.InstallDate.ToString("yyyy-MM-dd");
            ws.Cells[i + 2, 5].Value = d.Status.ToString();
        }

        ws.Cells.AutoFitColumns();
        package.SaveAs(new FileInfo(filePath));
    }
}
