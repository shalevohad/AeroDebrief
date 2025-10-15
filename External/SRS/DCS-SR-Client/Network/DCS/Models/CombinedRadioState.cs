using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.Models;
using RadioReceivingState = Ciribob.DCS.SimpleRadio.Standalone.Common.Models.RadioReceivingState;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models;

public struct CombinedRadioState
{
    public DCSPlayerRadioInfo RadioInfo;

    public RadioSendingState RadioSendingState;

    public RadioReceivingState[] RadioReceivingState;

    public int ClientCountConnected;

    public int[] TunedClients;
}