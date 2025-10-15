using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models;

public struct ClientListExport
{
    public ICollection<SRClientBase> Clients { get; set; }

    public string ServerVersion { get; set; }
}