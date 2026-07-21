namespace DevPlaytimeDesktop;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\DevPlaytimeDesktop.SingleInstance";
    private const string ActivationEventName = @"Local\DevPlaytimeDesktop.Activate";
    private System.Threading.Mutex? _singleInstanceMutex;
    private System.Threading.EventWaitHandle? _activationEvent;
    private System.Threading.CancellationTokenSource? _activationCancellation;
    private System.Threading.Tasks.Task? _activationListener;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        Localization.SetLanguage(ReadPreferredLanguage());
        _singleInstanceMutex = new System.Threading.Mutex(
            initiallyOwned: true,
            name: SingleInstanceMutexName,
            createdNew: out var createdNew);

        if (!createdNew)
        {
            if (!SignalExistingInstance())
            {
                System.Windows.MessageBox.Show(
                    Localization.T("App.AlreadyRunningActivationFailed"),
                    "DevPlaytime",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }

            Shutdown();
            return;
        }

        _activationEvent = new System.Threading.EventWaitHandle(
            initialState: false,
            mode: System.Threading.EventResetMode.AutoReset,
            name: ActivationEventName);
        _activationCancellation = new System.Threading.CancellationTokenSource();
        _activationListener = System.Threading.Tasks.Task.Run(() => ListenForActivation(_activationCancellation.Token));

        base.OnStartup(e);
    }

    private static string ReadPreferredLanguage()
    {
        try
        {
            var dataFile = Environment.GetEnvironmentVariable("DEVPLAYTIME_DATA_FILE")
                ?? System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DevPlaytime",
                    "devplaytime.json");
            if (System.IO.File.Exists(dataFile))
            {
                using var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(dataFile));
                if (document.RootElement.TryGetProperty("Language", out var languageElement))
                {
                    return Localization.NormalizeLanguage(languageElement.GetString());
                }
            }
        }
        catch
        {
            // Use the operating-system UI language when saved settings cannot be read.
        }

        return Localization.NormalizeLanguage(System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    }

    private static bool SignalExistingInstance()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var activationEvent = System.Threading.EventWaitHandle.OpenExisting(ActivationEventName);
                return activationEvent.Set();
            }
            catch (System.Threading.WaitHandleCannotBeOpenedException)
            {
                System.Threading.Thread.Sleep(50);
            }
            catch (System.UnauthorizedAccessException)
            {
                return false;
            }
        }

        return false;
    }

    private void ListenForActivation(System.Threading.CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _activationEvent is not null)
            {
                if (!_activationEvent.WaitOne(250)) continue;
                if (cancellationToken.IsCancellationRequested) break;

                Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                    new Action(() => ActivateMainWindow()));
            }
        }
        catch (System.ObjectDisposedException)
        {
            // The activation event is disposed during normal application shutdown.
        }
    }

    private void ActivateMainWindow(int remainingAttempts = 20)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;

        if (MainWindow is not DevPlaytimeDesktop.MainWindow window || !window.IsLoaded)
        {
            if (remainingAttempts <= 0) return;
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                new Action(() => ActivateMainWindow(remainingAttempts - 1)));
            return;
        }

        window.ActivateFromExternalLaunch();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _activationCancellation?.Cancel();
        try { _activationEvent?.Set(); }
        catch (System.ObjectDisposedException) { }
        try { _activationListener?.Wait(TimeSpan.FromSeconds(1)); }
        catch (System.AggregateException) { }
        _activationEvent?.Dispose();
        _activationCancellation?.Dispose();

        if (_singleInstanceMutex is not null)
        {
            try { _singleInstanceMutex.ReleaseMutex(); }
            catch (System.ApplicationException) { }
            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}
