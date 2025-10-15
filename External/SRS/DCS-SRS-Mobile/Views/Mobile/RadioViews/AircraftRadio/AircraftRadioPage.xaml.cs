using Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Utility;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Views.Mobile.RadioViews.AircraftRadio;

public partial class AircraftRadioPage : ContentPage
{
    private readonly IDispatcherTimer _updateTimer;

    public AircraftRadioPage()
    {
        BindingContext = new AircraftRadioPageViewModel();
        InitializeComponent();

        DeviceDisplay.Current.KeepScreenOn = true;

        _updateTimer = Application.Current.Dispatcher.CreateTimer();
        _updateTimer.Interval = TimeSpan.FromMilliseconds(100);

        _updateTimer.Tick += OnUpdateTimerOnTick;
    }

    private void OnUpdateTimerOnTick(object s, EventArgs e)
    {
        try
        {
            ((RadioViewModel)Radio1.BindingContext).RefreshView();
            ((RadioViewModel)Radio2.BindingContext).RefreshView();
            ((RadioViewModel)Radio3.BindingContext).RefreshView();
            ((RadioViewModel)Radio4.BindingContext).RefreshView();
            ((RadioViewModel)Radio5.BindingContext).RefreshView();
            ((RadioViewModel)Radio6.BindingContext).RefreshView();
            ((RadioViewModel)Radio7.BindingContext).RefreshView();
            ((RadioViewModel)Radio8.BindingContext).RefreshView();
            ((RadioViewModel)Radio9.BindingContext).RefreshView();
            ((RadioViewModel)Radio10.BindingContext).RefreshView();

            ((AircraftRadioPageViewModel)BindingContext).RefreshView();
        }
        catch (Exception ex)
        {
            //ignore
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _updateTimer?.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _updateTimer?.Stop();
    }

    private void Transmit_OnReleased(object sender, EventArgs e)
    {
        EventBus.Instance.PublishOnBackgroundThreadAsync(new PTTState { PTTPressed = false });
    }

    private void Transmit_OnPressed(object sender, EventArgs e)
    {
        EventBus.Instance.PublishOnBackgroundThreadAsync(new PTTState { PTTPressed = true });
    }
}