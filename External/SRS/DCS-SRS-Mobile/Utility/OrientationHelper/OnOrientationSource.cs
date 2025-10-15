using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Utility.OrientationHelper;

//SOURCE https://www.cayas.de/de/blog/responsive-layouts-for-dotnet-maui
public class OnOrientationSource : INotifyPropertyChanged
{
    private object _defaultValue;
    private object _landscapeValue;
    private object _portraitValue;

    public OnOrientationSource()
    {
        WeakReferenceMessenger.Default.Register<OnOrientationSource, OnOrientationExtension.OrientationChangedMessage>(
            this, OnOrientationChanged);
    }

    public object DefaultValue
    {
        get => _defaultValue;
        set
        {
            _defaultValue = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }

    public object PortraitValue
    {
        get => _portraitValue;
        set
        {
            _portraitValue = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }

    public object LandscapeValue
    {
        get => _landscapeValue;
        set
        {
            _landscapeValue = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }

    public object Value => DeviceDisplay.MainDisplayInfo.Orientation switch
    {
        DisplayOrientation.Portrait => PortraitValue ?? DefaultValue,
        DisplayOrientation.Landscape => LandscapeValue ?? DefaultValue,
        _ => DefaultValue
    };

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnOrientationChanged(OnOrientationSource r, OnOrientationExtension.OrientationChangedMessage m)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }
}