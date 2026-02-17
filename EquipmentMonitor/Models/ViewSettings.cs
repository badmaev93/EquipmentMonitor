using EquipmentMonitor.ViewModels;

namespace EquipmentMonitor.Models;

public class ViewSettings
{
    public ViewMode ViewMode { get; set; } = ViewMode.Columns;
    public SortField SortField { get; set; } = SortField.Status;
    public bool UseGroups { get; set; } = true;
}
