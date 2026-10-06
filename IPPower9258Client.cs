using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace IPPower9258.Remote;

public sealed class IPPower9258Client
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;

    public IPPower9258Client(
        string host,
        int port,
        string username,
        string password)
    {
        _host = host;
        _port = port;
        _username = username;
        _password = password;
    }

    public async Task<bool[]> GetPowerAsync(
        CancellationToken cancellationToken = default)
    {
        using TcpClient client =
            await OpenAuthenticatedConnectionAsync(cancellationToken);

        await using NetworkStream stream = client.GetStream();

        return await GetPowerFromStreamAsync(
            stream,
            cancellationToken);
    }

    public async Task<bool[]> SetPowerAsync(
        int outletNumber,
        bool turnOn,
        CancellationToken cancellationToken = default)
    {
        if (outletNumber < 1 || outletNumber > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outletNumber),
                "Le numéro de prise doit être compris entre 1 et 4.");
        }

        using TcpClient client =
            await OpenAuthenticatedConnectionAsync(cancellationToken);

        await using NetworkStream stream = client.GetStream();

        // 1. Lire l'état réel des quatre prises avant toute modification.
        bool[] currentStates =
            await GetPowerFromStreamAsync(
                stream,
                cancellationToken);

        // 2. Modifier uniquement la prise demandée.
        bool[] requestedStates =
            (bool[])currentStates.Clone();

        requestedStates[outletNumber - 1] = turnOn;

        // Si la prise est déjà dans l'état demandé,
        // aucune commande setpower n'est nécessaire.
        if (currentStates[outletNumber - 1] == turnOn)
        {
            return currentStates;
        }

        // Le firmware attend :
        // B4 B3 B2 B1
        //
        // Notre tableau contient :
        // [0] = prise 1
        // [1] = prise 2
        // [2] = prise 3
        // [3] = prise 4
        string p6 =
            $"{ToBit(requestedStates[3])}" +
            $"{ToBit(requestedStates[2])}" +
            $"{ToBit(requestedStates[1])}" +
            $"{ToBit(requestedStates[0])}";

        await WriteLineAsync(
            stream,
            $"setpower p6={p6}",
            cancellationToken);

        // Attendre le retour du prompt après setpower.
        await ReadUntilAsync(
            stream,
            "9258Telnet->",
            cancellationToken);

        // 3. Relire l'état physique retourné par le 9258.
        bool[] verifiedStates =
            await GetPowerFromStreamAsync(
                stream,
                cancellationToken);

        // 4. Vérifier la prise commandée.
        if (verifiedStates[outletNumber - 1] != turnOn)
        {
            throw new InvalidOperationException(
                $"La prise {outletNumber} n'a pas atteint l'état demandé.");
        }

        // 5. Vérifier que les trois autres prises
        // n'ont pas changé accidentellement.
        for (int i = 0; i < 4; i++)
        {
            if (i == outletNumber - 1)
            {
                continue;
            }

            if (verifiedStates[i] != currentStates[i])
            {
                throw new InvalidOperationException(
                    $"L'état de la prise {i + 1} a changé de manière inattendue.");
            }
        }

        return verifiedStates;
    }

    private async Task<TcpClient> OpenAuthenticatedConnectionAsync(
        CancellationToken cancellationToken)
    {
        var client = new TcpClient();

        try
        {
            await client.ConnectAsync(
                _host,
                _port,
                cancellationToken);

            NetworkStream stream = client.GetStream();

            // Attendre l'invite initiale du firmware V4.01.
            await ReadUntilAsync(
                stream,
                "9258Telnet->",
                cancellationToken);

            // Authentification :
            // username:password
            await WriteLineAsync(
                stream,
                $"{_username}:{_password}",
                cancellationToken);

            string loginResponse =
                await ReadUntilAsync(
                    stream,
                    "9258Telnet->",
                    cancellationToken);

            if (!loginResponse.Contains(
                    "Username and password is ok!",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Échec de l'authentification auprès de l'IP Power 9258.");
            }

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static async Task<bool[]> GetPowerFromStreamAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        await WriteLineAsync(
            stream,
            "getpower",
            cancellationToken);

        string response =
            await ReadUntilAsync(
                stream,
                "9258Telnet->",
                cancellationToken);

        Match match = Regex.Match(
            response,
            @"Power\s+Status\s*:\s*([01])\s+([01])\s+([01])\s+([01])",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            throw new InvalidOperationException(
                "Réponse getpower non reconnue.");
        }

        // Réponse firmware :
        // B4 B3 B2 B1
        bool b4 = match.Groups[1].Value == "1";
        bool b3 = match.Groups[2].Value == "1";
        bool b2 = match.Groups[3].Value == "1";
        bool b1 = match.Groups[4].Value == "1";

        return [b1, b2, b3, b4];
    }

    private static char ToBit(bool state)
    {
        return state ? '1' : '0';
    }

    private static async Task WriteLineAsync(
        NetworkStream stream,
        string text,
        CancellationToken cancellationToken)
    {
        byte[] data =
            Encoding.ASCII.GetBytes(text + "\r\n");

        await stream.WriteAsync(
            data,
            cancellationToken);

        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<string> ReadUntilAsync(
        NetworkStream stream,
        string expectedText,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[1024];
        var result = new StringBuilder();

        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        while (true)
        {
            int count =
                await stream.ReadAsync(
                    buffer,
                    timeout.Token);

            if (count == 0)
            {
                throw new IOException(
                    "Connexion fermée par l'IP Power 9258.");
            }

            result.Append(
                Encoding.ASCII.GetString(
                    buffer,
                    0,
                    count));

            if (result.ToString().Contains(
                    expectedText,
                    StringComparison.OrdinalIgnoreCase))
            {
                return result.ToString();
            }
        }
    }
}