using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Utility.OrientationHelper;

//SOURCE https://www.cayas.de/de/blog/responsive-layouts-for-dotnet-maui
[ContentProperty(nameof(Default))]
public class OnOrientationExtension : IMarkupExtension<BindingBase>
{
    static OnOrientationExtension()
    {
        DeviceDisplay.MainDisplayInfoChanged +=
            (_, _) => WeakReferenceMessenger.Default.Send(new OrientationChangedMessage());
    }

    public Type TypeConverter { get; set; }
    public object Default { get; set; }
    public object Landscape { get; set; }
    public object Portrait { get; set; }

    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        var typeConverter = TypeConverter != null ? (TypeConverter)Activator.CreateInstance(TypeConverter) : null;

        var orientationSource = new OnOrientationSource
            { DefaultValue = typeConverter?.ConvertFromInvariantString((string)Default) ?? Default };
        orientationSource.PortraitValue = Portrait == null
            ? orientationSource.DefaultValue
            : typeConverter?.ConvertFromInvariantString((string)Portrait) ?? Portrait;
        orientationSource.LandscapeValue = Landscape == null
            ? orientationSource.DefaultValue
            : typeConverter?.ConvertFromInvariantString((string)Landscape) ?? Landscape;

        return new Binding
        {
            Mode = BindingMode.OneWay,
            Path = nameof(OnOrientationSource.Value),
            Source = orientationSource
        };
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
    {
        return ProvideValue(serviceProvider);
    }

    public class OrientationChangedMessage
    {
    }
}