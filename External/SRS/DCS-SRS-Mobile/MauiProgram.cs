using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Platform.Compatibility;
using NLog;
using NLog.Extensions.Logging;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            //- Our documentation site: https://docs.microsoft.com/dotnet/communitytoolkit/maui
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("RobotoMono-VariableFont.ttf", "RobotoMono");
            })
            .ConfigureMauiHandlers(handlers =>
        {

        });


        // Add NLog for Logging
        builder.Logging.ClearProviders();
        //Add NLog as a logging provider?
        builder.Logging.AddNLog();

#if DEBUG
        var logger = LogManager.Setup().RegisterMauiLog()
            .LoadConfiguration(c => c.ForLogger(NLog.LogLevel.Debug).WriteToConsole())
            .GetCurrentClassLogger();
#else
        var logger = NLog.LogManager.Setup().RegisterMauiLog()
            .LoadConfiguration(c => c.ForLogger(NLog.LogLevel.Info).WriteToMauiLog())
            .GetCurrentClassLogger();
#endif

        return builder.Build();
    }
    
    
}


