using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        
        /*
         * SET STATICS on singletons BEFORE load to set up the platform specific bits
         */

        //https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/file-system-helpers?tabs=windows
        //Set path to use writeable app data directory
        GlobalSettingsStore.Path = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar;

        //Load from APK itself (magic loader using streaming memory)
        CachedAudioEffectProvider.CachedEffectsLoader = delegate(string name)
        {
            using var stream = FileSystem.OpenAppPackageFileAsync(name);
            var memStream = new MemoryStream();
            stream.Result.CopyTo(memStream);
            memStream.Position = 0;
            stream.Dispose();

            return memStream;
        };
    }
}