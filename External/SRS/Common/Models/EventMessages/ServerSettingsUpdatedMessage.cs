using System.Collections.Concurrent;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;

public class ServerSettingsUpdatedMessage
{
    public ServerSettingsUpdatedMessage(ConcurrentDictionary<string, string> settings)
    {
        Settings = settings;
    }

    public ConcurrentDictionary<string, string> Settings { get; }
}