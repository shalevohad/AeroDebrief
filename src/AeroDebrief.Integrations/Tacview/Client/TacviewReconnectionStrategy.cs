using AeroDebrief.Integrations.Tacview.Models;
using NLog;

namespace AeroDebrief.Integrations.Tacview.Client;

/// <summary>
/// Handles automatic reconnection to Tacview with exponential backoff
/// </summary>
public class TacviewReconnectionStrategy
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    /// <summary>
    /// Attempts to reconnect to Tacview with exponential backoff
    /// </summary>
    /// <param name="client">Tacview client to reconnect</param>
    /// <param name="config">Configuration with reconnection settings</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if reconnection was successful</returns>
    public async Task<bool> TryReconnectAsync(
        TacviewClient client, 
        TacviewConfiguration config,
        CancellationToken cancellationToken = default)
    {
        if (client == null)
            throw new ArgumentNullException(nameof(client));
        
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        
        int attempt = 0;
        int maxAttempts = config.MaxReconnectAttempts;
        
        // 0 means infinite attempts
        bool infiniteRetries = maxAttempts == 0;
        
        while (infiniteRetries || attempt < maxAttempts)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Logger.Info("Reconnection cancelled");
                return false;
            }
            
            attempt++;
            
            // Calculate delay with exponential backoff
            // Delay = baseInterval * attempt, capped at 60 seconds
            var delaySeconds = Math.Min(
                config.ReconnectIntervalSeconds * attempt,
                60
            );
            
            if (infiniteRetries)
            {
                Logger.Info($"Reconnection attempt {attempt} in {delaySeconds}s...");
            }
            else
            {
                Logger.Info($"Reconnection attempt {attempt}/{maxAttempts} in {delaySeconds}s...");
            }
            
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                
                Logger.Info($"Attempting to reconnect to Tacview ({config.Host}:{config.Port})...");
                await client.ConnectAsync(cancellationToken);
                
                Logger.Info("? Reconnection successful!");
                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Reconnection cancelled");
                return false;
            }
            catch (Exception ex)
            {
                if (infiniteRetries)
                {
                    Logger.Warn(ex, $"Reconnection attempt {attempt} failed, will retry");
                }
                else
                {
                    Logger.Warn(ex, $"Reconnection attempt {attempt}/{maxAttempts} failed");
                }
            }
        }
        
        Logger.Error($"? Failed to reconnect after {maxAttempts} attempts");
        return false;
    }
}
