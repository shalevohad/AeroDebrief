using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Properties;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Utils.ValueConverters;

internal class MicAvailabilityTooltipConverter : IValueConverter
{
    private static readonly ToolTip _noMicAvailable = BuildTooltip();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var micAvailable = (bool)value;
        if (micAvailable) return null;

        return _noMicAvailable;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static ToolTip BuildTooltip()
    {
        var NoMicAvailable = new ToolTip();
        var noMicAvailableContent = new StackPanel();

        noMicAvailableContent.Children.Add(new TextBlock
        {
            Text = Resources.ToolTipNoMic,
            FontWeight = FontWeights.Bold
        });
        noMicAvailableContent.Children.Add(new TextBlock
        {
            Text = Resources.ToolTipNoMicL1
        });
        noMicAvailableContent.Children.Add(new TextBlock
        {
            Text = Resources.ToolTipNoMicL2
        });

        NoMicAvailable.Content = noMicAvailableContent;
        return NoMicAvailable;
    }
}