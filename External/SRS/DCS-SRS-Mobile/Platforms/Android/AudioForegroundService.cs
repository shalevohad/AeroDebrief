using Android.App;
using Android.Content;
using Android.OS;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Mobile;

/// <summary>
///     Just by having this service active it keeps our app running and the audio playing correctly
/// </summary>
[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaPlayback)]
internal class AudioForegroundService : Service, IHandle<TCPClientStatusMessage>
{
    private PowerManager.WakeLock _wakeLock;
    private readonly string NOTIFICATION_CHANNEL_ID = "1801";
    private readonly string NOTIFICATION_CHANNEL_NAME = "DCS";
    private readonly int NOTIFICATION_ID = 1;

    public Task HandleAsync(TCPClientStatusMessage message, CancellationToken cancellationToken)
    {
        //todo
        UpdateNotification();

        if (!message.Connected)
        {
            ReturnWakeLock();
            //terminate
            StopSelf();
        }

        return Task.CompletedTask;
    }

    private void StartForegroundService()
    {
        var notifcationManager = GetSystemService(NotificationService) as NotificationManager;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O) CreateNotificationChannel(notifcationManager);

        StartForeground(NOTIFICATION_ID, GenerateNotification());
        EventBus.Instance.SubscribeOnUIThread(this);

        GetWakeLock();
    }

    private void GetWakeLock()
    {
        _wakeLock?.Release();

        var wakeFlags = WakeLockFlags.Partial;

        
        var pm =
            (PowerManager)Android.App.Application.Context.GetSystemService(PowerService);
        _wakeLock = pm.NewWakeLock(wakeFlags, typeof(AudioForegroundService).FullName);
        _wakeLock?.Acquire();
    }

    private void ReturnWakeLock()
    {
        _wakeLock?.Release();
    }

    private Notification GenerateNotification()
    {
        var intent = new Intent(Android.App.Application.Context, typeof(MainActivity));
        var pendingIntentFlags = Build.VERSION.SdkInt >= BuildVersionCodes.S
            ? PendingIntentFlags.UpdateCurrent |
              PendingIntentFlags.Mutable
            : PendingIntentFlags.UpdateCurrent;
        var pendingIntent = PendingIntent.GetActivity(Android.App.Application.Context, 0, intent, pendingIntentFlags);

        var status = SRSConnectionManager.Instance;

        var notification = new Notification.Builder(this, NOTIFICATION_CHANNEL_ID);
        notification.SetAutoCancel(false);
        notification.SetOngoing(true);
        notification.SetSmallIcon(Microsoft.Maui.Resource.Mipmap.appicon);
        notification.SetContentTitle("DCS-SRS");
        notification.SetContentText("DCS-SRS is running");
        notification.SetContentIntent(pendingIntent);
        return notification.Build();
    }

    private void CreateNotificationChannel(NotificationManager notificationMnaManager)
    {
        var channel = new NotificationChannel(NOTIFICATION_CHANNEL_ID, NOTIFICATION_CHANNEL_NAME,
            NotificationImportance.Low);
        notificationMnaManager.CreateNotificationChannel(channel);
    }

    public override IBinder OnBind(Intent intent)
    {
        return null;
    }

    private void UpdateNotification() {
        String text = "Some text that will update the notification";
    
        Notification notification = GenerateNotification();
    
        var notifcationManager = GetSystemService(Context.NotificationService) as NotificationManager;
        notifcationManager.Notify(NOTIFICATION_ID, notification);
    }

    public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
    {
        StartForegroundService();
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        EventBus.Instance.Unsubcribe(this);
        base.OnDestroy();
    }
}