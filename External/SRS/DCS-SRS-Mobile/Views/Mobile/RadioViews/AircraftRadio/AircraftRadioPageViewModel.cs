using Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Models.DCS.Models.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Singleton;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Utility;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Views.Mobile.RadioViews.AircraftRadio;

public class AircraftRadioPageViewModel : PropertyChangedBaseClass
{
    private static readonly Color RadioActiveTransmit = Color.Parse("#96FF6D");
    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

    public AircraftRadioPageViewModel()
    {
        RadioSelect = new Command(() => { RadioHelper.SelectRadio(0); }, () => IsAvailable);
        HotIntercomMicToggle = new Command(() =>
        {
            //TODO
            // ClientStateSingleton.Instance.DcsPlayerRadioInfo.IntercomHotMic =
            //     !ClientStateSingleton.Instance.PlayerUnitState.IntercomHotMic;
            NotifyPropertyChanged(nameof(HotIntercomTextColour));
        }, () => IsAvailable);
    }

    public bool IsAvailable
    {
        get
        {
            var radio = Radio;

            if (radio == null) return false;

            return radio.modulation != Modulation.DISABLED;
        }
    }

    public DCSRadio Radio => ClientStateSingleton.Instance.DcsPlayerRadioInfo.radios[0];

    public Command RadioSelect { get; set; }

    public Color RadioActiveFill
    {
        get
        {
            if (Radio == null || !IsAvailable) return Colors.Red;

            if (ClientStateSingleton.Instance.DcsPlayerRadioInfo.selected != 0)
                return Colors.Orange;

            if (ClientStateSingleton.Instance.RadioSendingState.IsSending &&
                ClientStateSingleton.Instance.RadioSendingState.SendingOn == 0)
                return RadioActiveTransmit;
            return Colors.Green;
        }
    }

    public bool VolumeEnabled
    {
        get
        {
            if (IsAvailable)
            {
                var currentRadio = Radio;

                if (currentRadio != null)
                    return true;
            }

            return false;
        }
    }

    public float Volume
    {
        get
        {
            var currentRadio = Radio;

            if (currentRadio != null) return currentRadio.volume * 100.0f;

            return 0f;
        }
        set
        {
            var currentRadio = Radio;

            if (currentRadio != null)
            {
                var clientRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[0];

                clientRadio.volume = value / 100.0f;
            }
        }
    }

    public Command HotIntercomMicToggle { get; set; }


    public Color HotIntercomTextColour
    {
        get
        {
            if (Radio == null || !IsAvailable) return Colors.Red;

            //TODO
            // if (ClientStateSingleton.Instance.PlayerUnitState.IntercomHotMic)
            //     return RadioActiveTransmit;

            return Colors.Red;
        }
    }

    public void RefreshView()
    {
        NotifyPropertyChanged(nameof(RadioActiveFill));
    }
}