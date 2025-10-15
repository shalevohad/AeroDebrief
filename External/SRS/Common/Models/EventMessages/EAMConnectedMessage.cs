namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;

public class EAMConnectedMessage
{
    public EAMConnectedMessage(int clientCoalition)
    {
        ClientCoalition = clientCoalition;
    }

    public int ClientCoalition { get; }
}