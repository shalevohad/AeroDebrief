# Tacview Client Reconnection Fix

## Problem
The Tacview client reconnection was failing due to race conditions between disconnect and reconnect operations. When the connection was lost:

1. The receive loop would detect the disconnection and call `DisconnectAsync()`
2. `CleanupConnectionAsync()` would dispose of all connection resources
3. The `TacviewIntegrationService` would trigger auto-reconnection via `TryReconnectAsync()`
4. The reconnection would call `ConnectAsync()` on the same client instance
5. **Race condition**: If the receive loop's cleanup wasn't complete, the new connection attempt could conflict with the ongoing cleanup

## Root Cause
- **No synchronization** between concurrent connect/disconnect operations
- **Receive loop calling DisconnectAsync()** could conflict with reconnection attempts
- **No state tracking** for "disconnecting in progress"
- **Cleanup not idempotent** - calling cleanup multiple times could cause issues

## Solution
Added proper synchronization and state management to `TacviewClient.cs`:

### 1. Connection Lock
```csharp
private readonly SemaphoreSlim _connectionLock = new(1, 1);
```
- Ensures only one connect or disconnect operation runs at a time
- Prevents race conditions between concurrent operations

### 2. Disconnecting State Flag
```csharp
private bool _isDisconnecting;
```
- Tracks when a disconnect is in progress
- Prevents duplicate disconnect operations
- Allows `ConnectAsync()` to wait for pending disconnect to complete

### 3. Updated ConnectAsync()
- **Acquires connection lock** before attempting connection
- **Waits for pending disconnect** to complete before connecting
- **Ensures clean state** by calling cleanup before connecting
- **Better error handling** for socket exceptions
- **Releases lock** in finally block to prevent deadlocks

### 4. Updated DisconnectAsync()
- **Acquires connection lock** before disconnecting
- **Checks _isDisconnecting flag** to prevent duplicate operations
- **Sets flag during disconnect** to block concurrent operations
- **Clears flag** in finally block to allow future connections

### 5. Updated ReceiveMessagesAsync()
- **Catches ObjectDisposedException** when reader is disposed during read
- **Checks _isDisconnecting flag** before triggering disconnect
- **Runs disconnect in background** to avoid deadlock with connection lock
- **Better error handling** for graceful shutdown

### 6. Renamed CleanupConnectionAsync()
Now `CleanupConnectionInternalAsync()` to clarify it's internal-only:
- **Assumes lock is held** by caller
- **Waits up to 2 seconds** for receive task to complete
- **Comprehensive error handling** for each resource disposal
- **Idempotent** - safe to call multiple times

## Testing
? Initial connection works correctly
? Disconnection cleans up resources properly
? Reconnection after disconnect works without errors
? Auto-reconnection after connection loss works
? Multiple rapid reconnect attempts don't cause race conditions
? Concurrent connect/disconnect operations are properly serialized

## Impact
- **Fixed**: Client reconnection now works reliably
- **Fixed**: No more race conditions during connect/disconnect
- **Fixed**: Auto-reconnection feature now functional
- **Improved**: Better error handling and logging
- **Improved**: More robust state management

## Migration Notes
No API changes - existing code using `TacviewClient` will work without modifications. The fixes are internal to the client implementation.

## Related Files
- `src\AeroDebrief.Integrations\Tacview\Client\TacviewClient.cs` - Main fix
- `src\AeroDebrief.Integrations\Tacview\Client\TacviewReconnectionStrategy.cs` - Uses fixed client
- `src\AeroDebrief.Integrations\Tacview\TacviewIntegrationService.cs` - Triggers reconnection
