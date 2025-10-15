using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;

public class UnitUpdateMessage
{
    private SRClientBase _unitUpdate;

    public SRClientBase UnitUpdate
    {
        get => _unitUpdate;
        set
        {
            if (value == null)
            {
                _unitUpdate = null;
            }
            else
            {
                var clone = value.DeepClone();
                _unitUpdate = clone;
            }
        }
    }

    public bool FullUpdate { get; set; }
}