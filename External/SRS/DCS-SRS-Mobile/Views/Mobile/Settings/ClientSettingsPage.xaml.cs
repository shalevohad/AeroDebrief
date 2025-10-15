namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Views.Mobile.Settings;

/// <summary>
///     Interaction logic for ClientSettings.xaml
/// </summary>
public partial class ClientSettingsPage : ContentPage
{
    public ClientSettingsPage()
    {
        InitializeComponent();

        //Checkbox toggles and others write their value when its set from a binding
        //which is silly.
        //this stops the set being fired until after its bound
        ((ClientSettingsViewModel)BindingContext).Loaded = true;
    }
}