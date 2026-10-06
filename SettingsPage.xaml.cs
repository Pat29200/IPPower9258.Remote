namespace IPPower9258.Remote;

public partial class SettingsPage : ContentPage
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

    public SettingsPage()
    {
        InitializeComponent();

        SaveButton.IsEnabled = true;

        Loaded += SettingsPage_Loaded;
        SaveButton.Clicked += SaveButton_Clicked;
    }

    private async void SettingsPage_Loaded(
        object? sender,
        EventArgs e)
    {
        HostEntry.Text = Preferences.Default.Get(
            HostKey,
            "192.168.0.18");

        PortEntry.Text = Preferences.Default.Get(
            PortKey,
            "23");

        UsernameEntry.Text = Preferences.Default.Get(
            UsernameKey,
            string.Empty);

        Outlet1NameEntry.Text = Preferences.Default.Get(
            Outlet1NameKey,
            DefaultOutlet1Name);

        Outlet2NameEntry.Text = Preferences.Default.Get(
            Outlet2NameKey,
            DefaultOutlet2Name);

        Outlet3NameEntry.Text = Preferences.Default.Get(
            Outlet3NameKey,
            DefaultOutlet3Name);

        Outlet4NameEntry.Text = Preferences.Default.Get(
            Outlet4NameKey,
            DefaultOutlet4Name);

        try
        {
            PasswordEntry.Text =
                await SecureStorage.Default.GetAsync(PasswordKey)
                ?? string.Empty;

            SettingsStatusLabel.Text =
                "Paramètres chargés";
        }
        catch (Exception)
        {
            PasswordEntry.Text =
                string.Empty;

            SettingsStatusLabel.Text =
                "Impossible de lire le mot de passe enregistré";
        }
    }

    private async void SaveButton_Clicked(
        object? sender,
        EventArgs e)
    {
        string host =
            HostEntry.Text?.Trim()
            ?? string.Empty;

        string portText =
            PortEntry.Text?.Trim()
            ?? string.Empty;

        string username =
            UsernameEntry.Text?.Trim()
            ?? string.Empty;

        string password =
            PasswordEntry.Text
            ?? string.Empty;

        string outlet1Name =
            Outlet1NameEntry.Text?.Trim()
            ?? string.Empty;

        string outlet2Name =
            Outlet2NameEntry.Text?.Trim()
            ?? string.Empty;

        string outlet3Name =
            Outlet3NameEntry.Text?.Trim()
            ?? string.Empty;

        string outlet4Name =
            Outlet4NameEntry.Text?.Trim()
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(host))
        {
            SettingsStatusLabel.Text =
                "L'adresse IP est obligatoire.";

            return;
        }

        if (!int.TryParse(portText, out int port) ||
            port < 1 ||
            port > 65535)
        {
            SettingsStatusLabel.Text =
                "Le port Telnet doit être compris entre 1 et 65535.";

            return;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            SettingsStatusLabel.Text =
                "Le nom d'utilisateur est obligatoire.";

            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            SettingsStatusLabel.Text =
                "Le mot de passe est obligatoire.";

            return;
        }

        if (string.IsNullOrWhiteSpace(outlet1Name) ||
            string.IsNullOrWhiteSpace(outlet2Name) ||
            string.IsNullOrWhiteSpace(outlet3Name) ||
            string.IsNullOrWhiteSpace(outlet4Name))
        {
            SettingsStatusLabel.Text =
                "Chaque prise doit avoir un nom.";

            return;
        }

        try
        {
            Preferences.Default.Set(
                HostKey,
                host);

            Preferences.Default.Set(
                PortKey,
                port.ToString());

            Preferences.Default.Set(
                UsernameKey,
                username);

            Preferences.Default.Set(
                Outlet1NameKey,
                outlet1Name);

            Preferences.Default.Set(
                Outlet2NameKey,
                outlet2Name);

            Preferences.Default.Set(
                Outlet3NameKey,
                outlet3Name);

            Preferences.Default.Set(
                Outlet4NameKey,
                outlet4Name);

            await SecureStorage.Default.SetAsync(
                PasswordKey,
                password);

            SettingsStatusLabel.Text =
                "Paramètres enregistrés";
        }
        catch (Exception)
        {
            SettingsStatusLabel.Text =
                "Erreur lors de l'enregistrement des paramètres";
        }
    }
}