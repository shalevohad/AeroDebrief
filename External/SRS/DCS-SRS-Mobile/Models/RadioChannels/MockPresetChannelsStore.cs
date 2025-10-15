namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Models.RadioChannels;

public class MockPresetChannelsStore : IPresetChannelsStore
{
    public IEnumerable<PresetChannel> LoadFromStore(string radioName, bool mids = false)
    {
        IList<PresetChannel> _presetChannels = new List<PresetChannel>();

        _presetChannels.Add(new PresetChannel
        {
            Text = 127.1 + "",
            Value = 127.1
        });

        _presetChannels.Add(new PresetChannel
        {
            Text = 127.1 + "",
            Value = 127.1
        });

        _presetChannels.Add(new PresetChannel
        {
            Text = 127.1 + "",
            Value = 127.1
        });

        return _presetChannels;
    }

    public string CreatePresetFile(string radioName)
    {
        //nothing
        return null;
    }
}