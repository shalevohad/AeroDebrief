using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Models.DCS.Models.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Singleton;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Utility;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Views.Mobile.RadioViews.SingleRadio;

public partial class SingleRadioPage : ContentPage
{
    private readonly IDispatcherTimer _updateTimer;
    public SingleRadioPage()
    {
        BindingContext = new RadioViewModel(1);
        InitializeComponent();
        DeviceDisplay.Current.KeepScreenOn = true;

  

        _updateTimer = Application.Current.Dispatcher.CreateTimer();
        _updateTimer.Interval = TimeSpan.FromMilliseconds(100);
        _updateTimer.Tick += (s, e) => ((RadioViewModel)BindingContext).RefreshView();

        ClientStateSingleton.Instance.DcsPlayerRadioInfo.radios[1].freq = 2.51e+8;
        ClientStateSingleton.Instance.DcsPlayerRadioInfo.radios[1].freqMode = DCSRadio.FreqMode.OVERLAY;
        ClientStateSingleton.Instance.DcsPlayerRadioInfo.radios[1].modulation = Modulation.AM;
        ClientStateSingleton.Instance.DcsPlayerRadioInfo.radios[1].volume = 1.0f;
        ClientStateSingleton.Instance.DcsPlayerRadioInfo.radios[1].freqMax = 3.51e+8;
        ClientStateSingleton.Instance.DcsPlayerRadioInfo.radios[1].freqMin = 1.51e+8;
        
        EventBus.Instance.PublishOnBackgroundThreadAsync(new UnitUpdateMessage()
        {
            FullUpdate = true,
            UnitUpdate = new SRClientBase()
            {
                AllowRecord = true,
                ClientGuid = ClientStateSingleton.Instance.ShortGUID,
                Coalition = 1,
                Name = "Android",
                LatLngPosition = new LatLngPosition(),
                RadioInfo = ClientStateSingleton.Instance.DcsPlayerRadioInfo.ConvertToRadioBase()
            }
        });

    }

    private void Button_OnPressed(object sender, EventArgs e)
    {
        EventBus.Instance.PublishOnBackgroundThreadAsync(new PTTState { PTTPressed = true });
    }

    private void Button_OnReleased(object sender, EventArgs e)
    {
        EventBus.Instance.PublishOnBackgroundThreadAsync(new PTTState { PTTPressed = false });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _updateTimer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _updateTimer.Stop();
    }
}