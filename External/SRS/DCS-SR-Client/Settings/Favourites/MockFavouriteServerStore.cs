﻿using System.Collections.Generic;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.Favourites;

public class MockFavouriteServerStore : IFavouriteServerStore
{
    public IEnumerable<ServerAddress> LoadFromStore()
    {
        yield return new ServerAddress("test 1", "123.456", null, true);
        yield return new ServerAddress("test 2", "123.456", null, false);
        yield return new ServerAddress("test 3", "123.456", null, false);
    }

    public bool SaveToStore(IEnumerable<ServerAddress> addresses)
    {
        return true;
    }
}