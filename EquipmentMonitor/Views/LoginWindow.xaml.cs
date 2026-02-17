using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Input;
using EquipmentMonitor.Models;
using EquipmentMonitor.Services;

namespace EquipmentMonitor.Views;

public partial class LoginWindow : Window
{
    private const string DefaultHost = "ep-round-smoke-a1a10c07-pooler.ap-southeast-1.aws.neon.tech";
    private const int DefaultPort = 5432;

    public UserSession? Session { get; private set; }
    public bool IsDemoMode { get; private set; }

    private AuthService? _authService;
    private bool _passwordVisible;
    private bool _suppressSync;

    public LoginWindow()
    {
        InitializeComponent();
        LoadSavedCredentials();
    }

    private void LoadSavedCredentials()
    {
        var (savedUser, savedPass) = CredentialStore.Load();
        if (savedUser is not null && savedPass is not null)
        {
            UsernameBox.Text = savedUser;
            PasswordBox.Password = savedPass;
            RememberCheck.IsChecked = true;

            Loaded += async (_, _) => await AutoLogin(savedUser, savedPass);
        }
        else
        {
            UsernameBox.Focus();
        }
    }

    private async Task AutoLogin(string username, string password)
    {
        LoginButton.IsEnabled = false;
        DemoButton.IsEnabled = false;
        ShowPanel("loading");
        LoadingText.Text = "Автоматический вход...";

        try
        {
            _authService = new AuthService(DefaultHost, DefaultPort);
            var (success, error) = await Task.Run(() =>
                _authService.ValidateCredentials(username, password, DefaultHost, DefaultPort));

            if (!success)
            {
                CredentialStore.Delete();
                ShowPanel("login");
                ShowLoginError("Сохранённые данные устарели, войдите заново");
                LoginButton.IsEnabled = true;
                DemoButton.IsEnabled = true;
                return;
            }

            LoadingText.Text = "Вход в систему...";
            Session = await Task.Run(() =>
                _authService.CreateSession(username, password, DefaultHost, DefaultPort));

            DialogResult = true;
            Close();
        }
        catch
        {
            ShowPanel("login");
            LoginButton.IsEnabled = true;
            DemoButton.IsEnabled = true;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowPanel(string panel)
    {
        LoginPanel.Visibility = Visibility.Collapsed;
        LoadingPanel.Visibility = Visibility.Collapsed;
        DiagPanel.Visibility = Visibility.Collapsed;

        switch (panel)
        {
            case "login":
                LoginPanel.Visibility = Visibility.Visible;
                break;
            case "loading":
                LoadingPanel.Visibility = Visibility.Visible;
                break;
            case "diag":
                DiagPanel.Visibility = Visibility.Visible;
                break;
        }
    }

    private void ShowLoginError(string message)
    {
        LoginErrorText.Text = message;
        LoginErrorText.Visibility = Visibility.Visible;
    }

    private void ClearLoginError()
    {
        LoginErrorText.Text = string.Empty;
        LoginErrorText.Visibility = Visibility.Collapsed;
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        ClearLoginError();

        var username = UsernameBox.Text.Trim();
        var password = _passwordVisible ? PasswordTextBox.Text : PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowLoginError("Введите логин и пароль");
            return;
        }

        LoginButton.IsEnabled = false;
        DemoButton.IsEnabled = false;
        ShowPanel("loading");
        LoadingText.Text = "Подключение к серверу...";

        try
        {
            _authService = new AuthService(DefaultHost, DefaultPort);

            var (success, error) = await Task.Run(() =>
                _authService.ValidateCredentials(username, password, DefaultHost, DefaultPort));

            if (!success)
            {
                if (IsConnectionError(error))
                {
                    await ShowConnectionDiagnostics(error);
                    return;
                }

                ShowPanel("login");
                ShowLoginError(error ?? "Ошибка аутентификации");
                LoginButton.IsEnabled = true;
                DemoButton.IsEnabled = true;
                return;
            }

            if (RememberCheck.IsChecked == true)
                CredentialStore.Save(username, password);
            else
                CredentialStore.Delete();

            LoadingText.Text = "Вход в систему...";
            Session = await Task.Run(() =>
                _authService.CreateSession(username, password, DefaultHost, DefaultPort));

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            if (IsConnectionError(ex.Message))
            {
                await ShowConnectionDiagnostics(ex.Message);
                return;
            }

            ShowPanel("login");
            ShowLoginError($"Ошибка: {ex.Message}");
            LoginButton.IsEnabled = true;
            DemoButton.IsEnabled = true;
        }
    }

    private void TogglePasswordVisibility_Click(object sender, RoutedEventArgs e)
    {
        _passwordVisible = !_passwordVisible;
        _suppressSync = true;

        if (_passwordVisible)
        {
            PasswordTextBox.Text = PasswordBox.Password;
            PasswordBox.Visibility = Visibility.Collapsed;
            PasswordTextBox.Visibility = Visibility.Visible;
            EyeOpen.Visibility = Visibility.Visible;
            EyeClosed.Visibility = Visibility.Collapsed;
            PasswordTextBox.Focus();
            PasswordTextBox.CaretIndex = PasswordTextBox.Text.Length;
        }
        else
        {
            PasswordBox.Password = PasswordTextBox.Text;
            PasswordTextBox.Visibility = Visibility.Collapsed;
            PasswordBox.Visibility = Visibility.Visible;
            EyeOpen.Visibility = Visibility.Collapsed;
            EyeClosed.Visibility = Visibility.Visible;
            PasswordBox.Focus();
        }

        _suppressSync = false;
    }

    private void PasswordTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_suppressSync)
            PasswordBox.Password = PasswordTextBox.Text;
    }

    private void DemoButton_Click(object sender, RoutedEventArgs e)
    {
        IsDemoMode = true;
        Session = new UserSession
        {
            Username = "demo",
            FullName = "Демо-пользователь",
            Department = "Демо",
            Role = "Оператор",
            ConnectionString = string.Empty
        };
        DialogResult = true;
        Close();
    }

    private void RetryConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel("login");
        LoginButton.IsEnabled = true;
        DemoButton.IsEnabled = true;
    }

    private static bool IsConnectionError(string? error)
    {
        if (string.IsNullOrEmpty(error)) return false;
        var lower = error.ToLowerInvariant();
        return lower.Contains("timeout") || lower.Contains("timed out")
            || lower.Contains("connection") || lower.Contains("host")
            || lower.Contains("network") || lower.Contains("socket")
            || lower.Contains("unreachable") || lower.Contains("dns")
            || lower.Contains("resolve") || lower.Contains("ssl")
            || lower.Contains("refused");
    }

    private async Task ShowConnectionDiagnostics(string? originalError)
    {
        LoadingText.Text = "Диагностика подключения...";
        ShowPanel("loading");

        var diagResult = await Task.Run(() => RunDiagnostics(originalError));

        DiagDetailsText.Text = diagResult.Summary;
        DiagReasonsText.Text = diagResult.Reasons;
        ShowPanel("diag");
    }

    private static (string Summary, string Reasons) RunDiagnostics(string? originalError)
    {
        var reasons = new List<string>();
        string summary;

        bool dnsOk = false;
        try
        {
            var addresses = System.Net.Dns.GetHostAddresses(DefaultHost);
            dnsOk = addresses.Length > 0;
        }
        catch { }

        bool pingOk = false;
        try
        {
            using var ping = new Ping();
            var reply = ping.Send("8.8.8.8", 3000);
            pingOk = reply.Status == IPStatus.Success;
        }
        catch { }

        bool portOk = false;
        if (dnsOk)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(DefaultHost, DefaultPort);
                portOk = connectTask.Wait(5000);
            }
            catch { }
        }

        if (!pingOk)
        {
            summary = "Отсутствует подключение к интернету.";
            reasons.Add("- ПК не имеет доступа к сети");
            reasons.Add("- Проверьте сетевой кабель или Wi-Fi");
            reasons.Add("- Если ПК в виртуальной машине — проверьте настройки сетевого адаптера (NAT/Bridged)");
        }
        else if (!dnsOk)
        {
            summary = "Не удалось разрешить адрес сервера БД.";
            reasons.Add("- Сервер DNS недоступен или настроен неверно");
            reasons.Add("- Имя хоста БД может быть неправильным");
        }
        else if (!portOk)
        {
            summary = $"Сервер {DefaultHost} найден, но порт {DefaultPort} недоступен.";
            reasons.Add("- Порт может быть заблокирован файрволом");
            reasons.Add("- Сервер PostgreSQL может быть выключен или перезагружается");
            reasons.Add("- Корпоративная сеть может блокировать исходящие подключения");
        }
        else
        {
            summary = $"Сетевое подключение установлено, но произошла ошибка авторизации.";
            if (originalError != null && originalError.ToLowerInvariant().Contains("ssl"))
            {
                reasons.Add("- Ошибка SSL/TLS соединения");
                reasons.Add("- Проверьте системные сертификаты");
            }
            else if (originalError != null && originalError.ToLowerInvariant().Contains("timeout"))
            {
                reasons.Add("- Сервер не отвечает вовремя (таймаут)");
                reasons.Add("- Медленное интернет-соединение");
                reasons.Add("- Высокая нагрузка на сервер");
            }
            else
            {
                reasons.Add($"- {originalError}");
                reasons.Add("- Проверьте логин и пароль");
                reasons.Add("- Проверьте доступность сервера базы данных");
            }
        }

        return (summary, string.Join("\n", reasons));
    }
}
