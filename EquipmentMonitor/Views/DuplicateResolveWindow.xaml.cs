using System.Windows;
using EquipmentMonitor.Models;

namespace EquipmentMonitor.Views;

public enum DuplicateAction
{
    Overwrite,
    KeepBoth,
    Skip
}

public partial class DuplicateResolveWindow : Window
{
    public DuplicateAction ChosenAction { get; private set; } = DuplicateAction.KeepBoth;
    public bool ApplyToAll => ApplyToAllCheckBox.IsChecked == true;

    public DuplicateResolveWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => BtnKeepBoth.Focus();
    }

    public void SetDevices(Device existing, Device importing)
    {
        SerialRun.Text = importing.SerialNumber;
        ExistingRun.Text = $"{existing.Name} ({existing.CategoryDisplayName}, {existing.StatusDisplayName})";
        ImportingRun.Text = $"{importing.Name} ({importing.CategoryDisplayName}, {importing.StatusDisplayName})";
    }

    private void BtnOverwrite_Click(object sender, RoutedEventArgs e)
    {
        ChosenAction = DuplicateAction.Overwrite;
        DialogResult = true;
        Close();
    }

    private void BtnKeepBoth_Click(object sender, RoutedEventArgs e)
    {
        ChosenAction = DuplicateAction.KeepBoth;
        DialogResult = true;
        Close();
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        ChosenAction = DuplicateAction.Skip;
        DialogResult = true;
        Close();
    }
}
