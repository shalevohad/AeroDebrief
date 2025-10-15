using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Views.Mobile.StatusBar;

public class StatusViewModel : PropertyChangedBaseClass, IHandle<VOIPStatusMessage>, IHandle<TCPClientStatusMessage>
{
    public StatusViewModel()
    {
        EventBus.Instance.SubscribeOnUIThread(this);
    }

    public Color TCPTextColour
    {
        get
        {
            if (SRSConnectionManager.Instance.TCPConnected) return Colors.Green;

            return Colors.Red;
        }
    }

    public string TCPText => $"TCP: {(SRSConnectionManager.Instance.TCPConnected ? "CONNECTED" : "DISCONNECTED")}";

    public Color VOIPTextColour
    {
        get
        {
            if (SRSConnectionManager.Instance.UDPConnected) return Colors.Green;

            return Colors.Red;
        }
    }

    public string VOIPText => $"VOIP: {(SRSConnectionManager.Instance.UDPConnected ? "CONNECTED" : "DISCONNECTED")}";

    public Task HandleAsync(TCPClientStatusMessage message, CancellationToken cancellationToken)
    {
        NotifyPropertyChanged(nameof(TCPTextColour));
        NotifyPropertyChanged(nameof(TCPText));

        return Task.CompletedTask;
    }

    public Task HandleAsync(VOIPStatusMessage message, CancellationToken cancellationToken)
    {
        NotifyPropertyChanged(nameof(VOIPTextColour));
        NotifyPropertyChanged(nameof(VOIPText));

        return Task.CompletedTask;
    }

    ~StatusViewModel()
    {
        EventBus.Instance.Unsubcribe(this);
    }
}