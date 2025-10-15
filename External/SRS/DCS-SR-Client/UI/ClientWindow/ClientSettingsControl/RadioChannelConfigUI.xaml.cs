using System.Windows;
using System.Windows.Controls;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientSettingsControl;

/// <summary>
///     Interaction logic for RadioChannelConfigUI.xaml
/// </summary>
public partial class RadioChannelConfigUi : UserControl
{
    //"VolumeValue" string must match the method name
    public static readonly DependencyProperty VolumeSliderDependencyProperty =
        DependencyProperty.Register("VolumeValue", typeof(float), typeof(RadioChannelConfigUi),
            new FrameworkPropertyMetadata((float)0)
        );

    public RadioChannelConfigUi()
    {
        InitializeComponent();
    }

    public float VolumeValue
    {
        set => SetValue(VolumeSliderDependencyProperty, value);
        get
        {
            var val = (float)GetValue(VolumeSliderDependencyProperty);
            return val;
        }
    }
}