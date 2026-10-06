namespace IPPower9258.Remote;

public partial class MainPage : ContentPage
{
    private const string HostKey = "IPPower9258_Host";
    private const string PortKey = "IPPower9258_Port";
    private const string UsernameKey = "IPPower9258_Username";
    private const string PasswordKey = "IPPower9258_Password";

    private const string Outlet1NameKey = "IPPower9258_Outlet1Name";
    private const string Outlet2NameKey = "IPPower9258_Outlet2Name";
    private const string Outlet3NameKey = "IPPower9258_Outlet3Name";
    private const string Outlet4NameKey = "IPPower9258_Outlet4Name";

    private const string DefaultOutlet1Name = "Caméra";
    private const string DefaultOutlet2Name = "PC télescope";
    private const string DefaultOutlet3Name = "Monture et Focuser";
    private const string DefaultOutlet4Name = "Résistance chauffante";

    private const int StartupDelaySeconds = 30;

    private static readonly Color OnColor =
        Color.FromArgb("#2E7D32");

    private static readonly Color OffColor =
        Color.FromArgb("#555555");

    private bool[] _states = new bool[4];
    private bool _statesKnown = false;
    private bool _operationInProgress = false;
    private bool _initialRefreshDone = false;

    private CancellationTokenSource? _startupCancellationTokenSource;
    private bool _startupInProgress = false;

    public MainPage()
    {
        InitializeComponent();

        SettingsButton.Clicked += SettingsButton_Clicked;
        RefreshButton.Clicked += RefreshButton_Clicked;

        StateButton1.Clicked += StateButton1_Clicked;
        StateButton2.Clicked += StateButton2_Clicked;
        StateButton3.Clicked += StateButton3_Clicked;
        StateButton4.Clicked += StateButton4_Clicked;

        StartupButton.Clicked += StartupButton_Clicked;
        CancelStartupButton.Clicked += CancelStartupButton_Clicked;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        LoadOutletNames();

        await UpdateConfigurationStateAsync();

        if (!_initialRefreshDone)
        {
            bool configurationComplete =
                await IsConfigurationCompleteAsync();

            if (configurationComplete)
            {
                _initialRefreshDone = true;
                await RefreshStatesAsync();
            }
        }
    }

    private void LoadOutletNames()
    {
        OutletNameLabel1.Text =
            Preferences.Default.Get(
                Outlet1NameKey,
                DefaultOutlet1Name);

        OutletNameLabel2.Text =
            Preferences.Default.Get(
                Outlet2NameKey,
                DefaultOutlet2Name);

        OutletNameLabel3.Text =
            Preferences.Default.Get(
                Outlet3NameKey,
                DefaultOutlet3Name);

        OutletNameLabel4.Text =
            Preferences.Default.Get(
                Outlet4NameKey,
                DefaultOutlet4Name);
    }

    private async Task UpdateConfigurationStateAsync()
    {
        bool configurationComplete =
            await IsConfigurationCompleteAsync();

        RefreshButton.IsEnabled =
            configurationComplete &&
            !_operationInProgress &&
            !_startupInProgress;

        bool controlsEnabled =
            configurationComplete &&
            _statesKnown &&
            !_operationInProgress &&
            !_startupInProgress;

        StateButton1.IsEnabled = controlsEnabled;
        StateButton2.IsEnabled = controlsEnabled;
        StateButton3.IsEnabled = controlsEnabled;
        StateButton4.IsEnabled = controlsEnabled;

        StartupButton.IsEnabled =
            controlsEnabled;

        SettingsButton.IsEnabled =
            !_operationInProgress &&
            !_startupInProgress;

        CancelStartupButton.IsVisible =
            _startupInProgress;

        CancelStartupButton.IsEnabled =
            _startupInProgress;

        if (!configurationComplete)
        {
            StatusLabel.Text =
                "Configuration incomplète";
        }
        else if (!_statesKnown &&
                 !_operationInProgress &&
                 !_startupInProgress)
        {
            StatusLabel.Text =
                "Prêt à lire l'état des prises";
        }
    }

    private async Task<bool> IsConfigurationCompleteAsync()
    {
        string host = Preferences.Default.Get(
            HostKey,
            string.Empty);

        string portText = Preferences.Default.Get(
            PortKey,
            "23");

        string username = Preferences.Default.Get(
            UsernameKey,
            string.Empty);

        string password;

        try
        {
            password =
                await SecureStorage.Default.GetAsync(PasswordKey)
                ?? string.Empty;
        }
        catch
        {
            password = string.Empty;
        }

        return
            !string.IsNullOrWhiteSpace(host) &&
            int.TryParse(portText, out int port) &&
            port >= 1 &&
            port <= 65535 &&
            !string.IsNullOrWhiteSpace(username) &&
            !string.IsNullOrEmpty(password);
    }

    private async Task<IPPower9258Client> CreateClientAsync()
    {
        string host = Preferences.Default.Get(
            HostKey,
            string.Empty);

        string portText = Preferences.Default.Get(
            PortKey,
            "23");

        string username = Preferences.Default.Get(
            UsernameKey,
            string.Empty);

        string password =
            await SecureStorage.Default.GetAsync(PasswordKey)
            ?? string.Empty;

        if (!int.TryParse(portText, out int port) ||
            port < 1 ||
            port > 65535)
        {
            throw new InvalidOperationException(
                "Port Telnet incorrect.");
        }

        return new IPPower9258Client(
            host,
            port,
            username,
            password);
    }

    private async void SettingsButton_Clicked(
        object? sender,
        EventArgs e)
    {
        await Navigation.PushAsync(
            new SettingsPage());
    }

    private async void RefreshButton_Clicked(
        object? sender,
        EventArgs e)
    {
        await RefreshStatesAsync();
    }

    private async Task RefreshStatesAsync()
    {
        if (_operationInProgress ||
            _startupInProgress)
        {
            return;
        }

        _operationInProgress = true;
        _statesKnown = false;

        await UpdateConfigurationStateAsync();

        StatusLabel.Text =
            "Lecture de l'état des prises...";

        try
        {
            IPPower9258Client client =
                await CreateClientAsync();

            bool[] states =
                await client.GetPowerAsync();

            ApplyStates(states);

            _statesKnown = true;

            StatusLabel.Text =
                "État des prises actualisé";
        }
        catch (OperationCanceledException)
        {
            StatusLabel.Text =
                "Délai d'attente dépassé";
        }
        catch (Exception ex)
        {
            StatusLabel.Text =
                $"Erreur : {ex.Message}";
        }
        finally
        {
            _operationInProgress = false;

            await UpdateConfigurationStateAsync();
        }
    }

    private async void StateButton1_Clicked(
        object? sender,
        EventArgs e)
    {
        await ToggleOutletAsync(1);
    }

    private async void StateButton2_Clicked(
        object? sender,
        EventArgs e)
    {
        await ToggleOutletAsync(2);
    }

    private async void StateButton3_Clicked(
        object? sender,
        EventArgs e)
    {
        await ToggleOutletAsync(3);
    }

    private async void StateButton4_Clicked(
        object? sender,
        EventArgs e)
    {
        await ToggleOutletAsync(4);
    }

    private async Task ToggleOutletAsync(
        int outletNumber)
    {
        if (_operationInProgress ||
            _startupInProgress ||
            !_statesKnown)
        {
            return;
        }

        int index = outletNumber - 1;

        bool requestedState =
            !_states[index];

        _operationInProgress = true;

        await UpdateConfigurationStateAsync();

        StatusLabel.Text = requestedState
            ? $"Mise sous tension de la prise {outletNumber}..."
            : $"Mise hors tension de la prise {outletNumber}...";

        try
        {
            IPPower9258Client client =
                await CreateClientAsync();

            bool[] verifiedStates =
                await client.SetPowerAsync(
                    outletNumber,
                    requestedState);

            ApplyStates(verifiedStates);

            _statesKnown = true;

            StatusLabel.Text = requestedState
                ? $"Prise {outletNumber} mise sur ON et vérifiée"
                : $"Prise {outletNumber} mise sur OFF et vérifiée";
        }
        catch (OperationCanceledException)
        {
            _statesKnown = false;

            StatusLabel.Text =
                "Délai d'attente dépassé";
        }
        catch (Exception ex)
        {
            _statesKnown = false;

            StatusLabel.Text =
                $"Erreur : {ex.Message}";
        }
        finally
        {
            _operationInProgress = false;

            await UpdateConfigurationStateAsync();
        }
    }

    private async void StartupButton_Clicked(
        object? sender,
        EventArgs e)
    {
        await StartObservatoryAsync();
    }

    private void CancelStartupButton_Clicked(
        object? sender,
        EventArgs e)
    {
        if (!_startupInProgress)
        {
            return;
        }

        CancelStartupButton.IsEnabled = false;

        StartupStatusLabel.Text =
            "Annulation en cours...";

        _startupCancellationTokenSource?.Cancel();
    }

    private async Task StartObservatoryAsync()
    {
        if (_operationInProgress ||
            _startupInProgress ||
            !_statesKnown)
        {
            return;
        }

        _startupInProgress = true;

        _startupCancellationTokenSource?.Dispose();
        _startupCancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            _startupCancellationTokenSource.Token;

        bool cameraWasInitiallyOn =
            _states[0];

        bool cameraTurnedOnBySequence =
            false;

        StartupStatusLabel.IsVisible = true;

        await UpdateConfigurationStateAsync();

        try
        {
            IPPower9258Client client =
                await CreateClientAsync();

            StartupStatusLabel.Text =
                "Vérification de l'état des prises...";

            bool[] currentStates =
                await client.GetPowerAsync(cancellationToken);

            ApplyStates(currentStates);
            _statesKnown = true;

            cameraWasInitiallyOn =
                currentStates[0];

            if (currentStates[1])
            {
                StartupStatusLabel.Text =
                    "PC télescope déjà sous tension";

                StatusLabel.Text =
                    "Aucune action nécessaire";

                return;
            }

            if (!currentStates[0])
            {
                StartupStatusLabel.Text =
                    "Mise sous tension de la caméra...";

                bool[] statesAfterCamera =
                    await client.SetPowerAsync(
                        1,
                        true,
                        cancellationToken);

                ApplyStates(statesAfterCamera);
                _statesKnown = true;

                cameraTurnedOnBySequence = true;

                for (int secondsRemaining = StartupDelaySeconds;
                     secondsRemaining > 0;
                     secondsRemaining--)
                {
                    StartupStatusLabel.Text =
                        $"Démarrage du PC télescope dans {secondsRemaining} s...";

                    await Task.Delay(
                        TimeSpan.FromSeconds(1),
                        cancellationToken);
                }
            }
            else
            {
                StartupStatusLabel.Text =
                    "Caméra déjà sous tension";
            }

            cancellationToken.ThrowIfCancellationRequested();

            StartupStatusLabel.Text =
                "Mise sous tension du PC télescope...";

            bool[] finalStates =
                await client.SetPowerAsync(
                    2,
                    true,
                    cancellationToken);

            ApplyStates(finalStates);
            _statesKnown = true;

            StartupStatusLabel.Text =
                "Démarrage terminé — Caméra et PC télescope sous tension";

            StatusLabel.Text =
                "Démarrage observatoire terminé";
        }
        catch (OperationCanceledException)
        {
            if (cameraTurnedOnBySequence &&
                !cameraWasInitiallyOn)
            {
                StartupStatusLabel.Text =
                    "Annulation : arrêt de la caméra...";

                try
                {
                    IPPower9258Client rollbackClient =
                        await CreateClientAsync();

                    bool[] rollbackStates =
                        await rollbackClient.SetPowerAsync(
                            1,
                            false);

                    ApplyStates(rollbackStates);
                    _statesKnown = true;

                    StartupStatusLabel.Text =
                        "Démarrage annulé — caméra arrêtée";

                    StatusLabel.Text =
                        "Démarrage observatoire annulé";
                }
                catch (Exception ex)
                {
                    _statesKnown = false;

                    StartupStatusLabel.Text =
                        "Démarrage annulé, mais l'état de la caméra doit être vérifié";

                    StatusLabel.Text =
                        $"Erreur lors du retour à l'état initial : {ex.Message}";
                }
            }
            else
            {
                StartupStatusLabel.Text =
                    "Démarrage annulé";

                StatusLabel.Text =
                    "Démarrage observatoire annulé";
            }
        }
        catch (Exception ex)
        {
            _statesKnown = false;

            StartupStatusLabel.Text =
                "Erreur pendant le démarrage";

            StatusLabel.Text =
                $"Erreur : {ex.Message}";
        }
        finally
        {
            _startupInProgress = false;

            _startupCancellationTokenSource?.Dispose();
            _startupCancellationTokenSource = null;

            await UpdateConfigurationStateAsync();
        }
    }

    private void ApplyStates(
        bool[] states)
    {
        if (states.Length != 4)
        {
            throw new InvalidOperationException(
                "Nombre d'états de prises incorrect.");
        }

        _states =
            (bool[])states.Clone();

        SetStateButton(
            StateButton1,
            _states[0]);

        SetStateButton(
            StateButton2,
            _states[1]);

        SetStateButton(
            StateButton3,
            _states[2]);

        SetStateButton(
            StateButton4,
            _states[3]);
    }

    private static void SetStateButton(
        Button button,
        bool isOn)
    {
        button.Text =
            isOn ? "ON" : "OFF";

        button.BackgroundColor =
            isOn ? OnColor : OffColor;

        button.TextColor =
            Colors.White;
    }
}