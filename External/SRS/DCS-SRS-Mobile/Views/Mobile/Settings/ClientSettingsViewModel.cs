using System.Globalization;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Singleton;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Views.Mobile.Settings;

public class ClientSettingsViewModel : PropertyChangedBaseClass
{
    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
    private readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public bool Loaded { get; set; } = false;


    /**
         * Global Settings
         */


    public bool AutoSelectChannel
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(
            ProfileSettingsKeys.AutoSelectPresetChannel);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.AutoSelectPresetChannel,
                value);
            NotifyPropertyChanged();
        }
    }

    public float PTTReleaseDelay
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.PTTReleaseDelay);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.PTTReleaseDelay, value);
            NotifyPropertyChanged();
        }
    }

    public float PTTStartDelay
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.PTTStartDelay);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.PTTStartDelay, value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioRxStartToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_Start);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_Start,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioRxEndToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_End);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_End,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioTxStartToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioTxEffects_Start);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioTxEffects_Start,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioTxEndToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_End);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_End,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioSoundEffectsToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioEffects);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioEffects, value);
            NotifyPropertyChanged();
        }
    }

    public bool RadioEffectsClippingToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping,
                value);
            NotifyPropertyChanged();
        }
    }

    public bool FMRadioToneToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.NATOTone);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(ProfileSettingsKeys.NATOTone, value);
            NotifyPropertyChanged();
        }
    }

    public double FMRadioToneVolume
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.NATOToneVolume)
            / double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.NATOToneVolume.ToString()],
                CultureInfo.InvariantCulture) * 100;
        set
        {
            if (!Loaded)
                return;
            var orig = double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.NATOToneVolume.ToString()],
                CultureInfo.InvariantCulture);
            var vol = orig * (value / 100);

            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.NATOToneVolume,
                (float)vol);
            NotifyPropertyChanged();
        }
    }

    public bool BackgroundRadioNoiseToggle
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys
            .RadioBackgroundNoiseEffect);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingBool(
                ProfileSettingsKeys.RadioBackgroundNoiseEffect, value);
            NotifyPropertyChanged();
        }
    }

    public double UHFEffectVolume
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.UHFNoiseVolume)
            / double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.UHFNoiseVolume.ToString()],
                CultureInfo.InvariantCulture) * 100;
        set
        {
            if (!Loaded)
                return;
            var orig = double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.UHFNoiseVolume.ToString()],
                CultureInfo.InvariantCulture);
            var vol = orig * (value / 100);

            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.UHFNoiseVolume,
                (float)vol);
            NotifyPropertyChanged();
        }
    }

    public double VHFEffectVolume
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.VHFNoiseVolume)
            / double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.VHFNoiseVolume.ToString()],
                CultureInfo.InvariantCulture) * 100;
        set
        {
            if (!Loaded)
                return;
            var orig = double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.VHFNoiseVolume.ToString()],
                CultureInfo.InvariantCulture);
            var vol = orig * (value / 100);

            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.VHFNoiseVolume,
                (float)vol);
            NotifyPropertyChanged();
        }
    }

    public double HFEffectVolume
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.HFNoiseVolume)
            / double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.HFNoiseVolume.ToString()],
                CultureInfo.InvariantCulture) * 100;
        set
        {
            if (!Loaded)
                return;
            var orig = double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.HFNoiseVolume.ToString()],
                CultureInfo.InvariantCulture);
            var vol = orig * (value / 100);

            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.HFNoiseVolume,
                (float)vol);
            NotifyPropertyChanged();
        }
    }

    public double FMEffectVolume
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.FMNoiseVolume)
            / double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.FMNoiseVolume.ToString()],
                CultureInfo.InvariantCulture) * 100;
        set
        {
            if (!Loaded)
                return;
            var orig = double.Parse(
                ProfileSettingsStore.DefaultSettingsProfileSettings[ProfileSettingsKeys.FMNoiseVolume.ToString()],
                CultureInfo.InvariantCulture);
            var vol = orig * (value / 100);

            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.FMNoiseVolume,
                (float)vol);
            NotifyPropertyChanged();
        }
    }

    /**
         * Radio Audio Balance
         */

    public float RadioChannel1
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio1Channel);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio1Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel2
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio2Channel);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio2Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel3
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio3Channel);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio3Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel4
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio4Channel);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio4Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel5
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio5Channel);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio5Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel6
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio6Channel);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio6Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel7
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio7Channel);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio7Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel8
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio8Channel);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio8Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel9
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio9Channel);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio9Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float RadioChannel10
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.Radio10Channel);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.Radio10Channel, value);
            NotifyPropertyChanged();
        }
    }

    public float Intercom
    {
        get => _globalSettings.ProfileSettingsStore.GetClientSettingFloat(ProfileSettingsKeys.IntercomChannel);
        set
        {
            if (!Loaded)
                return;
            _globalSettings.ProfileSettingsStore.SetClientSettingFloat(ProfileSettingsKeys.IntercomChannel, value);
            NotifyPropertyChanged();
        }
    }


    public string PlayerName
    {
        set
        {
            if (!Loaded)
                return;
            if (value != null) ClientStateSingleton.Instance.LastSeenName = value;
        }
        get => ClientStateSingleton.Instance.LastSeenName;
    }

    public uint PlayerID
    {
        set
        {
            if (!Loaded)
                return;
            if (value != null) ClientStateSingleton.Instance.DcsPlayerRadioInfo.unitId = value;
        }
        get => ClientStateSingleton.Instance.DcsPlayerRadioInfo.unitId;
    }
}