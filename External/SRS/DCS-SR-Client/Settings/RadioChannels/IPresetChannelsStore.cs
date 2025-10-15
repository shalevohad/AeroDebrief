using System.Collections.Generic;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.RadioChannels;

public interface IPresetChannelsStore
{
    IEnumerable<PresetChannel> LoadFromStore(string radioName, bool mids = false);

    string CreatePresetFile(string radioName);
}