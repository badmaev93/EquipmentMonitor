using System.ComponentModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using EquipmentMonitor.Models;
using EquipmentMonitor.ViewModels;

namespace EquipmentMonitor.Views;

public partial class MainWindow : Window
{
    private GridViewColumnHeader? _lastHeaderClicked;
    private ListSortDirection _lastDirection = ListSortDirection.Ascending;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void DeviceListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView listView && DataContext is MainViewModel vm)
        {
            vm.SelectedItems = listView.SelectedItems;
        }
    }

    private readonly Dictionary<GridViewColumn, string> _originalHeaders = new();

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader headerClicked) return;
        if (headerClicked.Role == GridViewColumnHeaderRole.Padding) return;

        ListSortDirection direction;
        if (headerClicked != _lastHeaderClicked)
        {
            direction = ListSortDirection.Ascending;
        }
        else
        {
            direction = _lastDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }

        var column = headerClicked.Column;
        if (!_originalHeaders.ContainsKey(column))
            _originalHeaders[column] = column.Header as string ?? "";

        var baseHeader = _originalHeaders[column];
        var sortBy = baseHeader switch
        {
            "Название" => nameof(Device.Name),
            "Серийный номер" => nameof(Device.SerialNumber),
            "Категория" => nameof(Device.CategoryDisplayName),
            "Дата установки" => nameof(Device.InstallDate),
            "Статус" => nameof(Device.StatusDisplayName),
            _ => null
        };

        if (sortBy is null) return;

        if (DataContext is MainViewModel vm)
        {
            var groupProp = vm.DevicesView.GroupDescriptions
                .OfType<PropertyGroupDescription>()
                .FirstOrDefault()?.PropertyName;

            vm.DevicesView.SortDescriptions.Clear();

            if (groupProp is not null)
                vm.DevicesView.SortDescriptions.Add(new SortDescription(groupProp, ListSortDirection.Ascending));

            vm.DevicesView.SortDescriptions.Add(new SortDescription(sortBy, direction));
            vm.DevicesView.Refresh();
        }

        if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked)
        {
            var prevColumn = _lastHeaderClicked.Column;
            if (_originalHeaders.TryGetValue(prevColumn, out var orig))
                prevColumn.Header = orig;
        }

        var arrow = direction == ListSortDirection.Ascending ? " ▲" : " ▼";
        column.Header = baseHeader + arrow;

        _lastHeaderClicked = headerClicked;
        _lastDirection = direction;
    }

    private void DeviceListView_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not ListView listView) return;

        var devices = new List<Device>();
        if (listView.SelectedItems.Count > 0)
        {
            foreach (var item in listView.SelectedItems)
            {
                if (item is Device d)
                    devices.Add(d);
            }
        }

        if (devices.Count == 0) return;

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            Converters = { new JsonStringEnumConverter() }
        };

        var jsonObjects = devices.Select(d => new
        {
            d.Category,
            CategoryName = d.CategoryDisplayName,
            d.Name,
            d.SerialNumber,
            InstallDate = d.InstallDate.ToString("dd.MM.yyyy"),
            d.Status,
            StatusName = d.StatusDisplayName
        });

        string json = devices.Count == 1
            ? JsonSerializer.Serialize(jsonObjects.First(), options)
            : JsonSerializer.Serialize(jsonObjects, options);

        Clipboard.SetText(json);
        ShowClipboardNotification();
    }

    private void ShowClipboardNotification()
    {
        ClipboardNotification.Visibility = Visibility.Visible;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            ClipboardNotification.Visibility = Visibility.Collapsed;
            timer.Stop();
        };
        timer.Start();
    }
}
