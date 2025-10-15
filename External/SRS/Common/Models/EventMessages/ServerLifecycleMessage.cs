using System.Collections.Generic;
using System.Collections.ObjectModel;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;

public class StartServerMessage
{
}

public class StopServerMessage
{
}

public class ServerStateMessage
{
    private readonly List<SRClientBase> _srClients;

    public ServerStateMessage(bool isRunning, List<SRClientBase> srClients, string guid = null)
    {
        _srClients = srClients;
        IsRunning = isRunning;
        DisconnectingClientGuid = guid;
    }

    public string DisconnectingClientGuid { get; private set; }

    //SUPER SAFE
    public ReadOnlyCollection<SRClientBase> Clients => new(_srClients);

    public bool IsRunning { get; }
    public int Count => _srClients.Count;
}

public class KickClientMessage
{
    public KickClientMessage(SRClientBase client)
    {
        Client = client;
    }

    public SRClientBase Client { get; }
}

public class BanClientMessage
{
    public BanClientMessage(SRClientBase client)
    {
        Client = client;
    }

    public SRClientBase Client { get; }
}

public class ServerSettingsChangedMessage
{
}

public class ServerFrequenciesChanged
{
    public string TestFrequencies { get; set; }
    public string GlobalLobbyFrequencies { get; set; }
    public string ServerRecordingFrequencies { get; set; }
}