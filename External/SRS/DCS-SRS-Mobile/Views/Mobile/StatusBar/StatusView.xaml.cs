using Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Views.Mobile.Settings;
using CommunityToolkit.Maui.Alerts;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Views.Mobile.StatusBar;

public partial class StatusView : ContentView
{
    public StatusView()
    {
        InitializeComponent();
    }


    private void Button_OnClicked(object sender, EventArgs e)
    {
        Toast.Make("Loading Settings").Show();
        Navigation.PushAsync(new ClientSettingsPage(), true);
    }
}