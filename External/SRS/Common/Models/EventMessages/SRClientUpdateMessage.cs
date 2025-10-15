using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;

public class SRClientUpdateMessage
{
    public SRClientUpdateMessage(SRClientBase srClient, bool connected = true)
    {
        SrClient = srClient;
        Connected = connected;
    }

    public SRClientBase SrClient { get; }
    public bool Connected { get; }
}