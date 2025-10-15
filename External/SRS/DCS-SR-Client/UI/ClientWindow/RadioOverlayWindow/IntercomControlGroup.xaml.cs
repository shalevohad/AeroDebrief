using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.RadioOverlayWindow;

/// <summary>
///     Interaction logic for IntercomControlGroup.xaml
/// </summary>
public partial class IntercomControlGroup : UserControl
{
    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
    private bool _dragging;

    public IntercomControlGroup()
    {
        InitializeComponent();
    }

    public int RadioId { private get; set; }

    private void RadioSelectSwitch(object sender, RoutedEventArgs e)
    {
        var currentRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[RadioId];

        if (currentRadio.modulation != Modulation.DISABLED)
            if (_clientStateSingleton.DcsPlayerRadioInfo.control ==
                DCSPlayerRadioInfo.RadioSwitchControls.HOTAS)
                _clientStateSingleton.DcsPlayerRadioInfo.selected = (short)RadioId;
    }

    private void RadioVolume_DragStarted(object sender, RoutedEventArgs e)
    {
        _dragging = true;
    }


    private void RadioVolume_DragCompleted(object sender, RoutedEventArgs e)
    {
        var currentRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[RadioId];

        if (currentRadio.modulation != Modulation.DISABLED)
            if (currentRadio.volMode == DCSRadio.VolumeMode.OVERLAY)
            {
                var clientRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[RadioId];

                clientRadio.volume = (float)RadioVolume.Value / 100.0f;
            }

        _dragging = false;
    }

    internal void RepaintRadioStatus()
    {
        var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

        if (dcsPlayerRadioInfo == null || !dcsPlayerRadioInfo.IsCurrent())
        {
            RadioActive.Fill = new SolidColorBrush(Colors.Red);

            RadioVolume.IsEnabled = false;

            //reset dragging just incase
            _dragging = false;
        }
        else
        {
            var currentRadio = dcsPlayerRadioInfo.radios[RadioId];
            var transmitting = _clientStateSingleton.RadioSendingState;
            var receiveState = _clientStateSingleton.RadioReceivingState[RadioId];
            if (receiveState != null && receiveState.IsReceiving)
            {
                RadioActive.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#96FF6D"));
            }
            else if (RadioId == dcsPlayerRadioInfo.selected ||
                     (transmitting.IsSending && transmitting.SendingOn == RadioId))
            {
                if (transmitting.IsSending && transmitting.SendingOn == RadioId)
                    RadioActive.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#96FF6D"));
                else
                    RadioActive.Fill = new SolidColorBrush(Colors.Green);
            }
            else
            {
                if (currentRadio.simul && dcsPlayerRadioInfo.simultaneousTransmission)
                    // if (transmitting.IsSending)
                    // {
                    //     RadioActive.Fill = new SolidColorBrush(Colors.LightBlue);
                    // }
                    // else
                    // {
                    RadioActive.Fill = new SolidColorBrush(Colors.DarkBlue);
                // }
                else
                    RadioActive.Fill = new SolidColorBrush(Colors.Orange);
            }

            if (currentRadio.modulation == Modulation.INTERCOM) //intercom
            {
                RadioLabel.Text = Properties.Resources.OverlayIntercom;

                RadioVolume.IsEnabled = currentRadio.volMode == DCSRadio.VolumeMode.OVERLAY;
            }
            else
            {
                RadioLabel.Text = Properties.Resources.OverlayNoIntercom;
                RadioActive.Fill = new SolidColorBrush(Colors.Red);
                RadioVolume.IsEnabled = false;
            }

            if (_dragging == false) RadioVolume.Value = currentRadio.volume * 100.0;
        }
    }
}