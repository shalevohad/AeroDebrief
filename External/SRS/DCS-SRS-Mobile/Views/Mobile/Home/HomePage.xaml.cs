using System.Net;
using System.Net.Sockets;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Singleton;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Views.Mobile.RadioViews.AircraftRadio;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Views.Mobile.RadioViews.SingleRadio;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using CommunityToolkit.Maui.Alerts;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile.Views.Mobile.Home;

public partial class HomePage : ContentPage, IHandle<TCPClientStatusMessage>
{
    private bool _isTransitioning;
    private bool connected;

    public HomePage()
    {
        InitializeComponent();

        var status = CheckAndRequestMicrophonePermission();

        /*
         *
         */
        EventBus.Instance.SubscribeOnUIThread(this);

        Address.Text = GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.LastServer)?.RawValue;

        Version.Text = $"Version: {UpdaterChecker.VERSION}";
    }

    public Task HandleAsync(TCPClientStatusMessage message, CancellationToken cancellationToken)
    {
        MainThread.BeginInvokeOnMainThread(() => { 
            if (message.Connected)
            {
                connected = true;
                ConnectDisconnect.Text = "Disconnect";
            }
            else
            {
                connected = false;
                ConnectDisconnect.Text = "Connect";
            }

            ConnectDisconnect.IsEnabled = true;
            
        });
        

        return Task.CompletedTask;
    }

    public async Task<PermissionStatus> CheckAndRequestMicrophonePermission()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();

        if (status == PermissionStatus.Granted)
            return status;

        if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
            // Prompt the user to turn on in settings
            // On iOS once a permission has been denied it may not be requested again from the application
            return status;

        if (Permissions.ShouldShowRationale<Permissions.Microphone>())
        {
            // Prompt the user with additional information as to why the permission is needed
        }

        status = await Permissions.RequestAsync<Permissions.Microphone>();

        return status;
    }

    private void OnStartClicked(object sender, EventArgs e)
    {
        if (connected)
        {
            EventBus.Instance.PublishOnBackgroundThreadAsync(new DisconnectRequestMessage());
            ConnectDisconnect.IsEnabled = false;
            ConnectDisconnect.Text = "Disconnecting...";
        }
        else
        {
            GlobalSettingsStore.Instance.SetClientSetting(GlobalSettingsKeys.LastServer,Address.Text.Trim());

            var ipEndPoint = GetConnectionIP(Address.Text);
            
            if (ipEndPoint != null)
            {
                ConnectDisconnect.IsEnabled = false;
                ConnectDisconnect.Text = "Connecting...";

                SRSConnectionManager.Instance.StartAndConnect(ipEndPoint);
            }
            else
            {
                DisplayAlert("Error", "Host or Invalid IP and port", "OK");
            }
        }
    }
    
    private int GetPortFromString(string input)
    {
        var addr = input.Trim();

        if (addr.Contains(":"))
        {
            int port;
            if (int.TryParse(addr.Split(':')[1], out port)) return port;

            throw new ArgumentException("specified port is  invalid");
        }

        return 5002;
    }

    private IPEndPoint GetConnectionIP(string input)
    {
        var addr = input.Trim().ToLowerInvariant();
        //strip port
        if (addr.Contains(':'))
        {
            addr = addr.Split(':')[0];
        }
        //process hostname
        var resolvedAddresses = Dns.GetHostAddresses(addr);
        var ip = resolvedAddresses.FirstOrDefault(xa =>
            xa.AddressFamily ==
            AddressFamily
                .InterNetwork); // Ensure we get an IPv4 address in case the host resolves to both IPv6 and IPv4

        if (ip != null)
        {
            try
            {
                int port = GetPortFromString(input);

                return new(ip, port);
            }
            catch (ArgumentException ex)
            {
                return null;
            }
        }
        return null;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _isTransitioning = false;
    }

    private void Navigate_Clicked(object sender, EventArgs e)
    {
        if (!_isTransitioning)
        {
            _isTransitioning = true;
         
        //    Toast.Make("Loading Single Radio").Show();
          
        }
        
        Navigation.PushAsync(new SingleRadioPage(), true);
    }

    private void AircraftRadio_OnClicked(object sender, EventArgs e)
    {
        if (!_isTransitioning)
        {
            _isTransitioning = true;
       //     ClientStateSingleton.Instance.DcsPlayerRadioInfo.LoadAircraftRadio();
            Toast.Make("Loading Aircraft Radio").Show();
            Navigation.PushAsync(new AircraftRadioPage(), true);
        }
    }
}