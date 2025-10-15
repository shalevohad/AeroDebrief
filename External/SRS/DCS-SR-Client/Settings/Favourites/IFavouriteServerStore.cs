using System.Collections.Generic;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.Favourites;

public interface IFavouriteServerStore
{
    IEnumerable<ServerAddress> LoadFromStore();

    bool SaveToStore(IEnumerable<ServerAddress> addresses);
}