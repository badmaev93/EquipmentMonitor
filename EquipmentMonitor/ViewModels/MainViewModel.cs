using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using EquipmentMonitor.Models;
using EquipmentMonitor.Services;
using EquipmentMonitor.ViewModels.Base;
using EquipmentMonitor.Views;
using Microsoft.Win32;

namespace EquipmentMonitor.ViewModels;

public enum ViewMode { List, Icons, Columns }
public enum SortField { Category, InstallDate, Name, Status }

public class MainViewModel : ViewModelBase
{
    private readonly IJsonPersistenceService _persistenceService;
    private readonly IDialogService _dialogService;
    private readonly PostgresDataService? _pgService;
    private readonly UserSession? _userSession;
    private string _statusBarText = "Готово";

    private Device? _selectedDevice;
    private string _searchText = string.Empty;
    private DeviceStatus? _selectedStatusFilter;
    private DeviceCategory? _selectedCategoryFilter;
    private DateTime? _dateFrom;
    private DateTime? _dateTo;

    private string _editName = string.Empty;
    private string _editSerialNumber = string.Empty;
    private DateTime _editInstallDate = DateTime.Today;
    private DeviceStatus _editStatus;
    private DeviceCategory _editCategory;

    private string _snapshotName = string.Empty;
    private string _snapshotSerialNumber = string.Empty;
    private DateTime _snapshotInstallDate = DateTime.Today;
    private DeviceStatus _snapshotStatus;
    private DeviceCategory _snapshotCategory;

    private ViewMode _currentViewMode = ViewMode.Columns;
    private SortField _currentSortField = SortField.Status;
    private bool _useGroups = true;
    private IList? _selectedItems;

    public ObservableCollection<Device> Devices { get; }
    public ICollectionView DevicesView { get; }

    public bool HasUnsavedChanges =>
        _selectedDevice is not null &&
        (_editName != _snapshotName ||
         _editSerialNumber != _snapshotSerialNumber ||
         _editInstallDate != _snapshotInstallDate ||
         _editStatus != _snapshotStatus ||
         _editCategory != _snapshotCategory);

    public Device? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (_selectedDevice == value) return;
            if (!TryDiscardChanges()) return;
            SetProperty(ref _selectedDevice, value);
            LoadSnapshot();
        }
    }

    public IList? SelectedItems
    {
        get => _selectedItems;
        set
        {
            if (SetProperty(ref _selectedItems, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(HasMultipleSelection));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasSelection => _selectedItems is not null && _selectedItems.Count > 0;
    public bool HasMultipleSelection => _selectedItems is not null && _selectedItems.Count > 1;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                DevicesView.Refresh();
        }
    }

    public DeviceStatus? SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
                DevicesView.Refresh();
        }
    }

    public DeviceCategory? SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
                DevicesView.Refresh();
        }
    }

    public DateTime? DateFrom
    {
        get => _dateFrom;
        set
        {
            if (SetProperty(ref _dateFrom, value))
            {
                if (_dateTo.HasValue && value.HasValue && _dateTo.Value < value.Value)
                    DateTo = value;
                DevicesView.Refresh();
            }
        }
    }

    public DateTime? DateTo
    {
        get => _dateTo;
        set
        {
            if (SetProperty(ref _dateTo, value))
                DevicesView.Refresh();
        }
    }

    public string EditName
    {
        get => _editName;
        set
        {
            if (SetProperty(ref _editName, value))
                OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    public string EditSerialNumber
    {
        get => _editSerialNumber;
        set
        {
            if (SetProperty(ref _editSerialNumber, value))
                OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    public DateTime EditInstallDate
    {
        get => _editInstallDate;
        set
        {
            if (SetProperty(ref _editInstallDate, value))
                OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    public DeviceStatus EditStatus
    {
        get => _editStatus;
        set
        {
            if (SetProperty(ref _editStatus, value))
                OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    public DeviceCategory EditCategory
    {
        get => _editCategory;
        set
        {
            if (SetProperty(ref _editCategory, value))
                OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    public ViewMode CurrentViewMode
    {
        get => _currentViewMode;
        set
        {
            if (SetProperty(ref _currentViewMode, value))
            {
                OnPropertyChanged(nameof(IsListView));
                OnPropertyChanged(nameof(IsIconsView));
                OnPropertyChanged(nameof(IsColumnsView));
            }
        }
    }

    public bool IsListView => _currentViewMode == ViewMode.List;
    public bool IsIconsView => _currentViewMode == ViewMode.Icons;
    public bool IsColumnsView => _currentViewMode == ViewMode.Columns;

    public SortField CurrentSortField
    {
        get => _currentSortField;
        set
        {
            if (SetProperty(ref _currentSortField, value))
                ApplySortingOrGrouping();
        }
    }

    public bool UseGroups
    {
        get => _useGroups;
        set
        {
            if (SetProperty(ref _useGroups, value))
                ApplySortingOrGrouping();
        }
    }

    public ICommand AddDeviceCommand { get; }
    public ICommand DeleteDeviceCommand { get; }
    public ICommand SaveDeviceCommand { get; }
    public ICommand ClearFilterCommand { get; }
    public ICommand ExitCommand { get; }

    public ICommand ImportJsonCommand { get; }
    public ICommand ImportXlsxCommand { get; }
    public ICommand ImportDbCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand ExportXlsxCommand { get; }
    public ICommand ExportDbCommand { get; }
    public ICommand ExportJsonSelectedCommand { get; }
    public ICommand ExportXlsxSelectedCommand { get; }
    public ICommand ExportDbSelectedCommand { get; }

    public ICommand SetViewModeCommand { get; }
    public ICommand SetSortFieldCommand { get; }
    public ICommand PrintCommand { get; }

    public ICommand PullCommand { get; }
    public ICommand CommitCommand { get; }
    public ICommand PushCommand { get; }

    public string StatusBarText
    {
        get => _statusBarText;
        set => SetProperty(ref _statusBarText, value);
    }

    public string? UserDisplayName => _userSession?.FullName;
    public string? UserRole => _userSession?.Role;

    public Array StatusValues => Enum.GetValues(typeof(DeviceStatus));
    public Array CategoryValues => Enum.GetValues(typeof(DeviceCategory));

    public StatusFilterItem[] StatusFilterItems { get; } =
    [
        new(null, "Все"),
        new(DeviceStatus.Working, "Работает"),
        new(DeviceStatus.Broken, "Сломано"),
        new(DeviceStatus.Decommissioned, "Списано")
    ];

    public CategoryFilterItem[] CategoryFilterItems { get; } =
    [
        new(null, "Все"),
        new(DeviceCategory.Server, "Серверы"),
        new(DeviceCategory.Printer, "Принтеры"),
        new(DeviceCategory.PC, "Компьютеры")
    ];

    public MainViewModel(IJsonPersistenceService persistenceService, IDialogService dialogService,
        PostgresDataService? pgService = null, UserSession? userSession = null)
    {
        _persistenceService = persistenceService;
        _dialogService = dialogService;
        _pgService = pgService;
        _userSession = userSession;

        var settings = _persistenceService.LoadSettings();
        _currentViewMode = settings.ViewMode;
        _currentSortField = settings.SortField;
        _useGroups = settings.UseGroups;

        var loaded = _persistenceService.Load();
        Devices = new ObservableCollection<Device>(loaded);

        DevicesView = CollectionViewSource.GetDefaultView(Devices);
        DevicesView.Filter = FilterDevices;
        ApplySortingOrGrouping();

        AddDeviceCommand = new RelayCommand(AddDevice);
        DeleteDeviceCommand = new RelayCommand(DeleteDevice, () => SelectedDevice is not null);
        SaveDeviceCommand = new RelayCommand(SaveDevice, () => HasUnsavedChanges);
        ClearFilterCommand = new RelayCommand(ClearFilter);
        ExitCommand = new RelayCommand(() => Application.Current.MainWindow?.Close());

        ImportJsonCommand = new RelayCommand(ImportJson);
        ImportXlsxCommand = new RelayCommand(ImportXlsx);
        ImportDbCommand = new RelayCommand(ImportDb);
        ExportJsonCommand = new RelayCommand(ExportJson);
        ExportXlsxCommand = new RelayCommand(ExportXlsx);
        ExportDbCommand = new RelayCommand(ExportDb);
        ExportJsonSelectedCommand = new RelayCommand(ExportJsonSelected, () => HasMultipleSelection);
        ExportXlsxSelectedCommand = new RelayCommand(ExportXlsxSelected, () => HasMultipleSelection);
        ExportDbSelectedCommand = new RelayCommand(ExportDbSelected, () => HasMultipleSelection);

        SetViewModeCommand = new RelayCommand(p =>
        {
            if (p is string s && Enum.TryParse<ViewMode>(s, out var mode))
                CurrentViewMode = mode;
        });
        SetSortFieldCommand = new RelayCommand(p =>
        {
            if (p is string s && Enum.TryParse<SortField>(s, out var field))
                CurrentSortField = field;
        });
        PrintCommand = new RelayCommand(PrintDeviceList);

        PullCommand = new RelayCommand(ExecutePull, () => _pgService is not null);
        CommitCommand = new RelayCommand(ExecuteCommit, () => _pgService is not null && Devices.Count > 0);
        PushCommand = new RelayCommand(ExecutePush, () => _pgService is not null);
    }

    private bool FilterDevices(object obj)
    {
        if (obj is not Device device)
            return false;

        if (_selectedStatusFilter.HasValue && device.Status != _selectedStatusFilter.Value)
            return false;

        if (_selectedCategoryFilter.HasValue && device.Category != _selectedCategoryFilter.Value)
            return false;

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var matchName = device.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
            var matchSerial = device.SerialNumber.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
            if (!matchName && !matchSerial)
                return false;
        }

        if (_dateFrom.HasValue && device.InstallDate.Date < _dateFrom.Value.Date)
            return false;

        if (_dateTo.HasValue && device.InstallDate.Date > _dateTo.Value.Date)
            return false;

        return true;
    }

    private void ApplySortingOrGrouping()
    {
        DevicesView.GroupDescriptions.Clear();
        DevicesView.SortDescriptions.Clear();

        string sortProperty = _currentSortField switch
        {
            SortField.Category => nameof(Device.CategoryDisplayName),
            SortField.InstallDate => nameof(Device.InstallDate),
            SortField.Name => nameof(Device.Name),
            SortField.Status => nameof(Device.StatusDisplayName),
            _ => nameof(Device.CategoryDisplayName)
        };

        if (_useGroups)
        {
            DevicesView.GroupDescriptions.Add(new PropertyGroupDescription(sortProperty));
        }
        else
        {
            DevicesView.SortDescriptions.Add(new SortDescription(sortProperty, ListSortDirection.Ascending));
        }

        DevicesView.Refresh();
    }

    private void LoadSnapshot()
    {
        if (_selectedDevice is null) return;
        EditName = _selectedDevice.Name;
        EditSerialNumber = _selectedDevice.SerialNumber;
        EditInstallDate = _selectedDevice.InstallDate;
        EditStatus = _selectedDevice.Status;
        EditCategory = _selectedDevice.Category;

        _snapshotName = _selectedDevice.Name;
        _snapshotSerialNumber = _selectedDevice.SerialNumber;
        _snapshotInstallDate = _selectedDevice.InstallDate;
        _snapshotStatus = _selectedDevice.Status;
        _snapshotCategory = _selectedDevice.Category;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    public bool TryDiscardChanges()
    {
        if (!HasUnsavedChanges) return true;
        var result = MessageBox.Show(
            "Есть несохранённые изменения. Сохранить?",
            "Несохранённые изменения",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            SaveDevice();
            return true;
        }
        return result == MessageBoxResult.No;
    }

    public bool CanClose()
    {
        return TryDiscardChanges();
    }

    private void AddDevice()
    {
        var device = _dialogService.ShowAddDeviceDialog();
        if (device is not null)
        {
            Devices.Add(device);
            SelectedDevice = device;
        }
    }

    private void DeleteDevice()
    {
        if (SelectedDevice is null) return;
        if (_dialogService.Confirm($"Удалить устройство \"{SelectedDevice.Name}\"?", "Подтверждение удаления"))
        {
            var dev = SelectedDevice;
            _snapshotName = _editName;
            _snapshotSerialNumber = _editSerialNumber;
            _snapshotInstallDate = _editInstallDate;
            _snapshotStatus = _editStatus;
            _snapshotCategory = _editCategory;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SelectedDevice = null;
            Devices.Remove(dev);
        }
    }

    private void SaveDevice()
    {
        if (SelectedDevice is null) return;
        SelectedDevice.Category = EditCategory;
        SelectedDevice.Name = EditName;
        SelectedDevice.SerialNumber = EditSerialNumber;
        SelectedDevice.InstallDate = EditInstallDate;
        SelectedDevice.Status = EditStatus;

        _snapshotName = EditName;
        _snapshotSerialNumber = EditSerialNumber;
        _snapshotInstallDate = EditInstallDate;
        _snapshotStatus = EditStatus;
        _snapshotCategory = EditCategory;
        OnPropertyChanged(nameof(HasUnsavedChanges));

        DevicesView.Refresh();
    }

    private void PrintDeviceList()
    {
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true) return;

        var doc = new FlowDocument
        {
            PagePadding = new Thickness(50, 40, 50, 60),
            ColumnWidth = double.MaxValue,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12
        };

        var title = new Paragraph(new Run("Панель мониторинга оборудования"))
        {
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 12)
        };
        doc.Blocks.Add(title);

        var grouped = GetGroupedDevices();

        switch (_currentViewMode)
        {
            case ViewMode.Icons:
                PrintIconsView(doc, grouped);
                break;
            case ViewMode.List:
                PrintListView(doc, grouped);
                break;
            case ViewMode.Columns:
            default:
                PrintColumnsView(doc, grouped);
                break;
        }

        doc.Blocks.Add(new Paragraph(new Run(DateTime.Now.ToString("dd.MM.yyyy")))
        {
            FontSize = 11,
            Margin = new Thickness(0, 20, 0, 0)
        });

        var pageSize = new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
        var basePaginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        basePaginator.PageSize = pageSize;

        var footerPaginator = new FooterDocumentPaginator(basePaginator, pageSize);
        printDialog.PrintDocument(footerPaginator, "Панель мониторинга оборудования");
    }

    private List<(string? GroupName, List<Device> Items)> GetGroupedDevices()
    {
        var result = new List<(string? GroupName, List<Device> Items)>();

        if (_useGroups && DevicesView.Groups is not null)
        {
            foreach (CollectionViewGroup group in DevicesView.Groups)
            {
                var items = group.Items.OfType<Device>().ToList();
                result.Add((group.Name?.ToString(), items));
            }
        }
        else
        {
            var items = new List<Device>();
            foreach (var item in DevicesView)
            {
                if (item is Device d) items.Add(d);
            }
            result.Add((null, items));
        }
        return result;
    }

    private static string GetCategoryIcon(DeviceCategory cat) => cat switch
    {
        DeviceCategory.Server => "\uE839",
        DeviceCategory.Printer => "\uE749",
        DeviceCategory.PC => "\uE7F4",
        _ => "\uE7F4"
    };

    private static void PrintIconsView(FlowDocument doc, List<(string? GroupName, List<Device> Items)> grouped)
    {
        foreach (var (groupName, items) in grouped)
        {
            if (groupName is not null)
            {
                doc.Blocks.Add(new Paragraph(new Bold(new Run(groupName)))
                {
                    FontSize = 14,
                    Background = Brushes.LightGray,
                    Padding = new Thickness(6, 4, 6, 4),
                    Margin = new Thickness(0, 8, 0, 4)
                });
            }

            var table = new Table { CellSpacing = 2 };
            int cols = 4;
            for (int c = 0; c < cols; c++)
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

            var rowGroup = new TableRowGroup();
            TableRow? currentRow = null;
            int colIndex = 0;

            foreach (var device in items)
            {
                if (colIndex % cols == 0)
                {
                    currentRow = new TableRow();
                    rowGroup.Rows.Add(currentRow);
                    colIndex = 0;
                }

                var cellContent = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(4) };
                cellContent.Inlines.Add(new Run(GetCategoryIcon(device.Category))
                {
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 24
                });
                cellContent.Inlines.Add(new LineBreak());
                cellContent.Inlines.Add(new Bold(new Run(device.Name)) { FontSize = 11 });
                cellContent.Inlines.Add(new LineBreak());
                cellContent.Inlines.Add(new Run(device.SerialNumber) { FontSize = 9, Foreground = Brushes.Gray });
                cellContent.Inlines.Add(new LineBreak());
                cellContent.Inlines.Add(new Run(device.StatusDisplayName) { FontSize = 9, FontStyle = FontStyles.Italic });

                currentRow!.Cells.Add(new TableCell(cellContent)
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(6)
                });
                colIndex++;
            }

            if (currentRow != null)
            {
                while (colIndex < cols)
                {
                    currentRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
                    colIndex++;
                }
            }

            table.RowGroups.Add(rowGroup);
            doc.Blocks.Add(table);
        }
    }

    private static void PrintListView(FlowDocument doc, List<(string? GroupName, List<Device> Items)> grouped)
    {
        foreach (var (groupName, items) in grouped)
        {
            if (groupName is not null)
            {
                doc.Blocks.Add(new Paragraph(new Bold(new Run(groupName)))
                {
                    FontSize = 14,
                    Background = Brushes.LightGray,
                    Padding = new Thickness(6, 4, 6, 4),
                    Margin = new Thickness(0, 8, 0, 4)
                });
            }

            foreach (var device in items)
            {
                var p = new Paragraph { Margin = new Thickness(4, 2, 4, 6) };
                p.Inlines.Add(new Bold(new Run(device.Name)));
                p.Inlines.Add(new Run($"   {device.StatusDisplayName}") { FontStyle = FontStyles.Italic });
                p.Inlines.Add(new LineBreak());
                p.Inlines.Add(new Run($"S/N: {device.SerialNumber}") { FontSize = 10, Foreground = Brushes.Gray });
                p.Inlines.Add(new LineBreak());
                p.Inlines.Add(new Run($"Установлено: {device.InstallDate:dd.MM.yyyy}") { FontSize = 10, Foreground = Brushes.Gray });
                doc.Blocks.Add(p);
            }
        }
    }

    private static void PrintColumnsView(FlowDocument doc, List<(string? GroupName, List<Device> Items)> grouped)
    {
        var borderBrush = Brushes.Black;
        var borderThickness = new Thickness(0.5);

        foreach (var (groupName, items) in grouped)
        {
            if (groupName is not null)
            {
                doc.Blocks.Add(new Paragraph(new Bold(new Run(groupName)))
                {
                    FontSize = 14,
                    Padding = new Thickness(6, 4, 6, 4),
                    Margin = new Thickness(0, 8, 0, 4)
                });
            }

            var table = new Table { CellSpacing = 0, BorderBrush = borderBrush, BorderThickness = borderThickness };
            table.Columns.Add(new TableColumn { Width = new GridLength(200) });
            table.Columns.Add(new TableColumn { Width = new GridLength(130) });
            table.Columns.Add(new TableColumn { Width = new GridLength(90) });
            table.Columns.Add(new TableColumn { Width = new GridLength(100) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });

            var headerGroup = new TableRowGroup();
            var headerRow = new TableRow();
            headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Название")))) { Padding = new Thickness(4), BorderBrush = borderBrush, BorderThickness = borderThickness });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Серийный №")))) { Padding = new Thickness(4), BorderBrush = borderBrush, BorderThickness = borderThickness });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Категория")))) { Padding = new Thickness(4), BorderBrush = borderBrush, BorderThickness = borderThickness });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Дата уст.")))) { Padding = new Thickness(4), BorderBrush = borderBrush, BorderThickness = borderThickness });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Статус")))) { Padding = new Thickness(4), BorderBrush = borderBrush, BorderThickness = borderThickness });
            headerGroup.Rows.Add(headerRow);
            table.RowGroups.Add(headerGroup);

            var bodyGroup = new TableRowGroup();
            foreach (var device in items)
            {
                var row = new TableRow();
                row.Cells.Add(new TableCell(new Paragraph(new Run(device.Name))) { Padding = new Thickness(4), BorderBrush = borderBrush, BorderThickness = borderThickness });
                row.Cells.Add(new TableCell(new Paragraph(new Run(device.SerialNumber))) { Padding = new Thickness(4), BorderBrush = borderBrush, BorderThickness = borderThickness });
                row.Cells.Add(new TableCell(new Paragraph(new Run(device.CategoryDisplayName))) { Padding = new Thickness(4), BorderBrush = borderBrush, BorderThickness = borderThickness });
                row.Cells.Add(new TableCell(new Paragraph(new Run(device.InstallDate.ToString("dd.MM.yyyy")))) { Padding = new Thickness(4), BorderBrush = borderBrush, BorderThickness = borderThickness });
                row.Cells.Add(new TableCell(new Paragraph(new Run(device.StatusDisplayName))) { Padding = new Thickness(4), BorderBrush = borderBrush, BorderThickness = borderThickness });
                bodyGroup.Rows.Add(row);
            }
            table.RowGroups.Add(bodyGroup);
            doc.Blocks.Add(table);
        }
    }

    private class FooterDocumentPaginator : DocumentPaginator
    {
        private readonly DocumentPaginator _inner;
        private readonly Size _pageSize;

        public FooterDocumentPaginator(DocumentPaginator inner, Size pageSize)
        {
            _inner = inner;
            _pageSize = pageSize;
        }

        public override bool IsPageCountValid => _inner.IsPageCountValid;
        public override int PageCount => _inner.PageCount;
        public override Size PageSize
        {
            get => _pageSize;
            set { }
        }
        public override IDocumentPaginatorSource Source => _inner.Source;

        public override DocumentPage GetPage(int pageNumber)
        {
            var page = _inner.GetPage(pageNumber);
            var container = new ContainerVisual();
            container.Children.Add(page.Visual);

            var footer = new DrawingVisual();
            using (var ctx = footer.RenderOpen())
            {
                var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

                var pageText = new FormattedText(
                    $"Стр. {pageNumber + 1} из {_inner.PageCount}",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface, 10, Brushes.Gray,
                    VisualTreeHelper.GetDpi(footer).PixelsPerDip);

                double y = _pageSize.Height - 30;
                ctx.DrawText(pageText, new Point(_pageSize.Width - 50 - pageText.Width, y));
            }
            container.Children.Add(footer);

            return new DocumentPage(container, _pageSize, page.BleedBox, page.ContentBox);
        }
    }

    private void ExecutePull()
    {
        if (_pgService is null) return;
        try
        {
            StatusBarText = "Pull: загрузка данных из БД...";
            var pulled = _pgService.Pull();

            _snapshotName = _editName;
            _snapshotSerialNumber = _editSerialNumber;
            _snapshotInstallDate = _editInstallDate;
            _snapshotStatus = _editStatus;
            _snapshotCategory = _editCategory;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SelectedDevice = null;

            Devices.Clear();
            foreach (var d in pulled)
                Devices.Add(d);
            DevicesView.Refresh();

            StatusBarText = $"Pull: загружено {pulled.Count} устройств";
            MessageBox.Show($"Загружено устройств из БД: {pulled.Count}", "Pull", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusBarText = "Pull: ошибка";
            MessageBox.Show($"Ошибка Pull: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteCommit()
    {
        if (_pgService is null) return;
        try
        {
            StatusBarText = "Commit: отправка данных в staging...";
            var (inserted, updated, rejected) = _pgService.Commit(Devices);
            StatusBarText = $"Commit: +{inserted} / ~{updated} / !{rejected}";
            MessageBox.Show(
                $"Commit завершён:\n  Вставлено: {inserted}\n  Обновлено: {updated}\n  Отклонено: {rejected}",
                "Commit", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusBarText = "Commit: ошибка";
            MessageBox.Show($"Ошибка Commit: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecutePush()
    {
        if (_pgService is null) return;
        try
        {
            StatusBarText = "Push: запуск полного ETL...";
            var results = _pgService.Push();
            var sb = new StringBuilder("Push (ETL) результаты:\n\n");
            foreach (var (step, status, details) in results)
            {
                sb.AppendLine($"  {step}: {status}");
                if (!string.IsNullOrEmpty(details))
                    sb.AppendLine($"    {details}");
            }
            StatusBarText = "Push: ETL завершён";
            MessageBox.Show(sb.ToString(), "Push — ETL", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusBarText = "Push: ошибка";
            MessageBox.Show($"Ошибка Push: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearFilter()
    {
        SearchText = string.Empty;
        SelectedStatusFilter = null;
        SelectedCategoryFilter = null;
        DateFrom = null;
        DateTo = null;
    }

    public void SaveData()
    {
        _persistenceService.Save(Devices);
        _persistenceService.SaveSettings(new Models.ViewSettings
        {
            ViewMode = _currentViewMode,
            SortField = _currentSortField,
            UseGroups = _useGroups
        });
    }

    // --- Import / Export ---

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        Converters = { new JsonStringEnumConverter() }
    };

    private void ImportJson()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "JSON файлы (*.json)|*.json",
            Title = "Импорт из JSON"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json = File.ReadAllText(dlg.FileName, Encoding.UTF8);
            var devices = JsonSerializer.Deserialize<List<Device>>(json, JsonOptions);
            if (devices is null || devices.Count == 0)
            {
                MessageBox.Show("Файл не содержит данных.", "Импорт", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            int added = MergeDevices(devices);
            MessageBox.Show($"Импортировано устройств: {added}", "Импорт", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка импорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportJson()
    {
        ExportJsonInternal(Devices.ToList());
    }

    private void ExportJsonSelected()
    {
        var selected = GetSelectedDevices();
        if (selected.Count == 0) return;
        ExportJsonInternal(selected);
    }

    private void ExportJsonInternal(List<Device> devices)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "JSON файлы (*.json)|*.json",
            Title = "Экспорт в JSON",
            FileName = "devices.json"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json = JsonSerializer.Serialize(devices, JsonOptions);
            File.WriteAllText(dlg.FileName, json, new UTF8Encoding(true));
            MessageBox.Show($"Экспортировано устройств: {devices.Count}", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportXlsx()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Excel файлы (*.xlsx)|*.xlsx",
            Title = "Импорт из Excel"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var devices = ExcelService.ImportFromXlsx(dlg.FileName);
            if (devices.Count == 0)
            {
                MessageBox.Show("Файл не содержит данных.", "Импорт", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            int added = MergeDevices(devices);
            MessageBox.Show($"Импортировано устройств: {added}", "Импорт", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка импорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportXlsx()
    {
        ExportXlsxInternal(Devices.ToList());
    }

    private void ExportXlsxSelected()
    {
        var selected = GetSelectedDevices();
        if (selected.Count == 0) return;
        ExportXlsxInternal(selected);
    }

    private void ExportXlsxInternal(List<Device> devices)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Excel файлы (*.xlsx)|*.xlsx",
            Title = "Экспорт в Excel",
            FileName = "devices.xlsx"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            ExcelService.ExportToXlsx(dlg.FileName, devices);
            MessageBox.Show($"Экспортировано устройств: {devices.Count}", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportDb()
    {
        var connectionString = PromptConnectionString("Импорт из базы данных");
        if (connectionString is null) return;
        try
        {
            var devices = DatabaseService.Import(connectionString);
            if (devices.Count == 0)
            {
                MessageBox.Show("Таблица не содержит данных.", "Импорт", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            int added = MergeDevices(devices);
            MessageBox.Show($"Импортировано устройств: {added}", "Импорт", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка импорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportDb()
    {
        ExportDbInternal(Devices.ToList());
    }

    private void ExportDbSelected()
    {
        var selected = GetSelectedDevices();
        if (selected.Count == 0) return;
        ExportDbInternal(selected);
    }

    private void ExportDbInternal(List<Device> devices)
    {
        var connectionString = PromptConnectionString("Экспорт в базу данных");
        if (connectionString is null) return;
        try
        {
            DatabaseService.Export(connectionString, devices);
            MessageBox.Show($"Экспортировано устройств: {devices.Count}", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private List<Device> GetSelectedDevices()
    {
        if (_selectedItems is null) return [];
        return _selectedItems.Cast<Device>().ToList();
    }

    private static string? PromptConnectionString(string title)
    {
        var window = new Window
        {
            Title = title,
            Width = 500,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow,
            ResizeMode = ResizeMode.NoResize
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "Введите строку подключения к базе данных (SQLite, например: Data Source=devices.db):",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });
        var textBox = new System.Windows.Controls.TextBox
        {
            Text = "Data Source=devices.db",
            Margin = new Thickness(0, 0, 0, 12)
        };
        panel.Children.Add(textBox);

        var btnPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var okBtn = new System.Windows.Controls.Button
        {
            Content = "OK",
            Width = 80,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        okBtn.Click += (_, _) => { window.DialogResult = true; window.Close(); };
        var cancelBtn = new System.Windows.Controls.Button
        {
            Content = "Отмена",
            Width = 80,
            IsCancel = true
        };
        btnPanel.Children.Add(okBtn);
        btnPanel.Children.Add(cancelBtn);
        panel.Children.Add(btnPanel);

        window.Content = panel;
        return window.ShowDialog() == true ? textBox.Text : null;
    }

    private int MergeDevices(List<Device> importedDevices)
    {
        _snapshotName = _editName;
        _snapshotSerialNumber = _editSerialNumber;
        _snapshotInstallDate = _editInstallDate;
        _snapshotStatus = _editStatus;
        _snapshotCategory = _editCategory;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        SelectedDevice = null;

        DuplicateAction? applyAllAction = null;
        int addedCount = 0;

        foreach (var imported in importedDevices)
        {
            var existing = Devices.FirstOrDefault(d =>
                !string.IsNullOrEmpty(d.SerialNumber) &&
                d.SerialNumber.Equals(imported.SerialNumber, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                Devices.Add(imported);
                addedCount++;
                continue;
            }

            DuplicateAction action;
            if (applyAllAction.HasValue)
            {
                action = applyAllAction.Value;
            }
            else
            {
                var dialog = new DuplicateResolveWindow
                {
                    Owner = Application.Current.MainWindow
                };
                dialog.SetDevices(existing, imported);
                if (dialog.ShowDialog() != true)
                    continue;

                action = dialog.ChosenAction;
                if (dialog.ApplyToAll)
                    applyAllAction = action;
            }

            switch (action)
            {
                case DuplicateAction.Overwrite:
                    existing.Name = imported.Name;
                    existing.Category = imported.Category;
                    existing.Status = imported.Status;
                    existing.InstallDate = imported.InstallDate;
                    existing.SerialNumber = imported.SerialNumber;
                    addedCount++;
                    break;
                case DuplicateAction.KeepBoth:
                    Devices.Add(imported);
                    addedCount++;
                    break;
                case DuplicateAction.Skip:
                    break;
            }
        }

        DevicesView.Refresh();
        return addedCount;
    }
}

public record StatusFilterItem(DeviceStatus? Value, string DisplayName);
public record CategoryFilterItem(DeviceCategory? Value, string DisplayName);
