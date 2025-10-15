namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.LotATC.Models;

// {"controller":{"altitude":34,"isFlying":false,"latitude":45.03800822794648,"longitude":39.18813534903283}}
public class LotATCMessageWrapper
{
    public LotATCPosition controller;

    public LotATCLineOfSightResponse los;

    //class not struct so we get Nulls
    public class LotATCPosition
    {
        public double altitude;
        public bool isFlying;
        public double latitude;
        public double longitude;
    }

    public class LotATCLineOfSightResponse
    {
        public string clientId;
        public bool see; //visible
    }
}