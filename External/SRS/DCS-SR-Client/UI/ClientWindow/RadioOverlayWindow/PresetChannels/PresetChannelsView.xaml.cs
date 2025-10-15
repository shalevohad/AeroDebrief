using System.Windows.Controls;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.RadioOverlayWindow.PresetChannels;

/// <summary>
///     Interaction logic for PresetChannelsView.xaml
/// </summary>
public partial class PresetChannelsView : UserControl
{
    public PresetChannelsView()
    {
        InitializeComponent();

        //set to window width
        FrequencyDropDown.Width = Width;
    }
}