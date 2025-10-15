namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Models.RadioChannels;

public class PresetChannel
{
    public string Text { get; set; }
    public object Value { get; set; }
    public int Channel { get; set; }

    public override string ToString()
    {
        return Text;
    }
}