using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.Core.Playback
{
    /// <summary>Handles seeking operations</summary>
    public sealed class SeekController : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly object _seekLock = new();
        private volatile bool _seekRequested;
        private TimeSpan _seekPosition;
        private DateTime _lastSeekTime = DateTime.MinValue;
        
        public bool IsUserSeeking { get; private set; }

        public void SeekTo(TimeSpan position, TimeSpan totalDuration)
        {
            position = TimeSpan.FromTicks(Math.Clamp(position.Ticks, 0, totalDuration.Ticks));
            
            lock (_seekLock)
            {
                _seekPosition = position;
                _lastSeekTime = DateTime.UtcNow;
            }
            _seekRequested = true;
            
            Logger.Info($"Seek requested to position: {position} (total duration: {totalDuration})");
        }

        public void SetUserSeeking(bool seeking) 
        {
            IsUserSeeking = seeking;
            Logger.Debug($"User seeking: {seeking}");
            
            // If user stopped seeking, clear any pending seek request that might be stale
            if (!seeking)
            {
                lock (_seekLock)
                {
                    // Clear seek request if it's more than 1 second old
                    if (_seekRequested && (DateTime.UtcNow - _lastSeekTime).TotalSeconds > 1.0)
                    {
                        _seekRequested = false;
                        Logger.Debug("Cleared stale seek request");
                    }
                }
            }
        }

        public bool HandleSeekIfRequested(List<AudioPacketMetadata> packets, ref int currentIndex, ref DateTime startTime)
        {
            if (!_seekRequested || packets.Count == 0) return false;

            TimeSpan seekPos;
            DateTime seekTime;
            lock (_seekLock)
            {
                seekPos = _seekPosition;
                seekTime = _lastSeekTime;
            }
            _seekRequested = false;

            // Don't process very old seek requests
            if ((DateTime.UtcNow - seekTime).TotalSeconds > 2.0)
            {
                Logger.Debug("Ignoring stale seek request");
                return false;
            }

            var recordingStart = packets[0].Timestamp;
            var targetTime = recordingStart.Add(seekPos);
            
            // Find the best packet index for the target time
            int newIndex = FindBestPacketIndex(packets, targetTime);
            
            // Only update if we're actually changing position significantly
            if (Math.Abs(newIndex - currentIndex) < 5)
            {
                Logger.Debug($"Seek target too close to current position, ignoring (current: {currentIndex}, target: {newIndex})");
                return false;
            }
            
            currentIndex = newIndex;
            
            // Reset timing to account for the new position
            var actualSeekTime = packets[currentIndex].Timestamp - recordingStart;
            startTime = DateTime.UtcNow.Subtract(actualSeekTime);
            
            Logger.Info($"Seek executed - jumped to packet index {currentIndex} at position {actualSeekTime} (requested: {seekPos})");
            return true;
        }

        private int FindBestPacketIndex(List<AudioPacketMetadata> packets, DateTime targetTime)
        {
            // Binary search for the best packet index
            int left = 0;
            int right = packets.Count - 1;
            int bestIndex = 0;
            
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                
                if (packets[mid].Timestamp <= targetTime)
                {
                    bestIndex = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }
            
            // Ensure we don't go past the end
            return Math.Min(bestIndex, packets.Count - 1);
        }

        public void Dispose()
        {
            // Nothing to dispose for this implementation
        }
    }
}