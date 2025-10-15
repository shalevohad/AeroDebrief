using System;
using System.Globalization;
using System.Windows.Data;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Utils.ValueConverters;

internal class BooleanInverterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !(bool)value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}