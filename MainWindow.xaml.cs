using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StreamKartoteka.Core;
using StreamKartoteka.Services;
using StreamKartoteka.ViewModels;

namespace StreamKartoteka;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly TwitchIrcClient _twitchClient = new();
    private bool _connectionOperationInProgress;
    private int _demoRun;
    private readonly string? _previewCapturePath;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        var settings = AppSettingsStore.Load();
        ChannelTextBox.Text = settings.Channel;
        LoginTextBox.Text = settings.Login;

        _twitchClient.MessageReceived += TwitchClient_MessageReceived;
        _twitchClient.StatusChanged += TwitchClient_StatusChanged;
        _twitchClient.ConnectionFaulted += TwitchClient_ConnectionFaulted;

        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;

        var arguments = Environment.GetCommandLineArgs();
        var captureIndex = Array.FindIndex(arguments, argument =>
            string.Equals(argument, "--capture", StringComparison.OrdinalIgnoreCase));
        if (captureIndex >= 0 && captureIndex + 1 < arguments.Length)
        {
            _previewCapturePath = arguments[captureIndex + 1];
        }

        if (arguments.Any(argument =>
                string.Equals(argument, "--demo", StringComparison.OrdinalIgnoreCase)) ||
            _previewCapturePath is not null)
        {
            Loaded += MainWindow_Loaded;
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadDemoMessages();

        if (_previewCapturePath is not null)
        {
            Dispatcher.BeginInvoke(
                () => CapturePreviewAndClose(_previewCapturePath),
                DispatcherPriority.ApplicationIdle);
        }
    }

    private void CapturePreviewAndClose(string outputPath)
    {
        try
        {
            RootLayout.UpdateLayout();
            var width = Math.Max(1, (int)Math.Ceiling(RootLayout.ActualWidth));
            var height = Math.Max(1, (int)Math.Ceiling(RootLayout.ActualHeight));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var drawing = new DrawingVisual();
            using (var context = drawing.RenderOpen())
            {
                context.DrawRectangle(
                    new VisualBrush(RootLayout) { Stretch = Stretch.Fill },
                    null,
                    new Rect(0, 0, width, height));
            }
            bitmap.Render(drawing);

            var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            encoder.Save(file);
        }
        finally
        {
            Close();
        }
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_connectionOperationInProgress)
        {
            return;
        }

        _connectionOperationInProgress = true;
        ConnectButton.IsEnabled = false;

        try
        {
            if (_viewModel.IsConnected || _twitchClient.IsConnected)
            {
                await _twitchClient.DisconnectAsync();
                _viewModel.IsConnected = false;
                _viewModel.StatusText = "Odpojeno";
                return;
            }

            var channel = ChannelTextBox.Text.Trim().TrimStart('#');
            var login = LoginTextBox.Text.Trim();
            var token = TokenPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(channel) ||
                string.IsNullOrWhiteSpace(login) ||
                string.IsNullOrWhiteSpace(token))
            {
                ShowFriendlyError("Vyplňte kanál, Twitch účet i OAuth token. Token se neukládá.");
                return;
            }

            AppSettingsStore.Save(channel, login);
            await _twitchClient.ConnectAsync(channel, login, token);
            _viewModel.IsConnected = true;
            _viewModel.StatusText = $"Ověřuji přístup k #{channel.ToLowerInvariant()}…";
        }
        catch (Exception exception)
        {
            _viewModel.IsConnected = false;
            ShowFriendlyError(exception.Message);
        }
        finally
        {
            _connectionOperationInProgress = false;
            ConnectButton.IsEnabled = true;
        }
    }

    private void DemoButton_Click(object sender, RoutedEventArgs e) => LoadDemoMessages();

    private void LoadDemoMessages()
    {
        _demoRun++;
        var now = DateTimeOffset.Now;
        var demoMessages = new[]
        {
            ("pixelova_vila", "PixelováVíla", "Ahoj! Jak ses dostal k téhle hře?", "#A970FF"),
            ("matej_cz", "MatějCZ", "Bude dneska ještě ranked?", "#00B5AD"),
            ("kafe_a_kod", "KafeAKód", "Ten poslední souboj byl skvělý 😄", "#F97316"),
            ("lucie_live", "LucieLive", "Můžeš prosím ukázat nastavení grafiky?", "#EC4899"),
            ("pixelova_vila", "PixelováVíla", "A mimochodem, zvuk je dnes perfektní.", "#A970FF"),
            ("oldschool_pavel", "OldschoolPavel", "Jaký ovladač používáš?", "#3B82F6"),
            ("zelena_sova", "ZelenáSova", "Pozdravíš prosím Karla? Má narozeniny!", "#84CC16"),
            ("viktor_88", "Viktor_88", "Je tohle první průchod bez spoilerů?", "#EAB308"),
            ("nela_art", "NelaArt", "Ten design postavy se mi moc líbí.", "#F43F5E"),
            ("matej_cz", "MatějCZ", "Držím palce do dalšího kola!", "#00B5AD"),
            ("retro_rabbit", "RetroRabbit", "Kde najdu seznam modů?", "#8B5CF6"),
            ("tomas_na_ceste", "TomášNaCestě", "Dorazil jsem pozdě — co mi uteklo?", "#14B8A6")
        };

        for (var index = 0; index < demoMessages.Length; index++)
        {
            var item = demoMessages[index];
            _viewModel.AddMessage(new ChatEntry(
                $"demo-{_demoRun}-{index}",
                item.Item1,
                item.Item2,
                item.Item3,
                now.AddSeconds(index - demoMessages.Length),
                item.Item4));
        }

        if (!_viewModel.IsConnected)
        {
            _viewModel.StatusText = "Demo · bez připojení";
        }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e) => _viewModel.NextMessage();

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.UserCount == 0)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            "Opravdu odstranit všechny zprávy a celou frontu?",
            "Vyčistit kartotéku",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.Clear();
        }
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        const string url = "https://dev.twitch.tv/docs/authentication/getting-tokens-oauth/";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            ShowFriendlyError($"Nápovědu se nepodařilo otevřít: {exception.Message}");
        }
    }

    private void TwitchClient_MessageReceived(object? sender, ChatEntry message) =>
        Dispatcher.BeginInvoke(() => _viewModel.AddMessage(message));

    private void TwitchClient_StatusChanged(object? sender, string status) =>
        Dispatcher.BeginInvoke(() =>
        {
            _viewModel.StatusText = status;
            _viewModel.IsConnected = true;
        });

    private void TwitchClient_ConnectionFaulted(object? sender, string error) =>
        Dispatcher.BeginInvoke(() => _ = HandleConnectionFaultAsync(error));

    private async Task HandleConnectionFaultAsync(string error)
    {
        await _twitchClient.DisconnectAsync();
        _viewModel.IsConnected = false;
        _viewModel.StatusText = "Chyba připojení";
        ShowFriendlyError(error);
    }

    private void ShowFriendlyError(string message)
    {
        _viewModel.StatusText = message;
        MessageBox.Show(this, message, "ZombieLilčin konsolidátor odpovědí", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e) =>
        AppSettingsStore.Save(ChannelTextBox.Text.Trim().TrimStart('#'), LoginTextBox.Text.Trim());

    private async void MainWindow_Closed(object? sender, EventArgs e) =>
        await _twitchClient.DisposeAsync();
}
