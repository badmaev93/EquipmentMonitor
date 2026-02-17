using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using EquipmentMonitor.Services;
using EquipmentMonitor.ViewModels;
using EquipmentMonitor.Views;

namespace EquipmentMonitor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var culture = new CultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

        base.OnStartup(e);

        var loginWindow = new LoginWindow();
        if (loginWindow.ShowDialog() != true || loginWindow.Session is null)
        {
            Shutdown();
            return;
        }

        var session = loginWindow.Session;
        PostgresDataService? pgService = null;

        if (!loginWindow.IsDemoMode && !string.IsNullOrEmpty(session.ConnectionString))
        {
            pgService = new PostgresDataService(session.ConnectionString);
        }

        var persistenceService = new JsonPersistenceService();
        var dialogService = new DialogService();
        MainViewModel mainViewModel;
        try
        {
            mainViewModel = new MainViewModel(persistenceService, dialogService, pgService, session);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // Автоматический Pull данных из БД при входе
        if (pgService != null)
        {
            try
            {
                var pulled = pgService.Pull();
                if (pulled.Count > 0)
                {
                    mainViewModel.Devices.Clear();
                    foreach (var d in pulled)
                        mainViewModel.Devices.Add(d);
                    mainViewModel.StatusBarText = $"Загружено {pulled.Count} устройств из БД";
                }
            }
            catch (Exception ex)
            {
                mainViewModel.StatusBarText = $"Ошибка загрузки из БД: {ex.Message}";
            }
        }

        var titleSuffix = loginWindow.IsDemoMode
            ? "Демо-режим"
            : $"{session.FullName} ({session.Role})";

        var mainWindow = new MainWindow
        {
            DataContext = mainViewModel,
            Title = $"Панель мониторинга оборудования — {titleSuffix}"
        };
        mainWindow.Closing += (_, args) =>
        {
            if (!mainViewModel.CanClose())
            {
                args.Cancel = true;
                return;
            }
            mainViewModel.SaveData();
        };
        mainWindow.Closed += (_, _) => Shutdown();
        mainWindow.Show();
    }
}
