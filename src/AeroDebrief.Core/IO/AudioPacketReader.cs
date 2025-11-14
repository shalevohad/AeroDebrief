using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AeroDebrief.Core.Models;
using AeroDebrief.Core.Playback;

namespace AeroDebrief.Core.IO
{
    /// <summary>
    /// DEPRECATED: This class has been replaced by FilePacketSource + FilePlaybackPipeline.
    /// This stub exists only for backward compatibility during migration.
    /// DO NOT USE THIS CLASS IN NEW CODE.
    /// </summary>
    [Obsolete("AudioPacketReader is deprecated. Use FilePacketSource and FilePlaybackPipeline instead.")]
    public class AudioPacketReader : IDisposable
    {
        public event Action? PlaybackStarted;
        public event Action? PlaybackStopped;
        public event Action? PlaybackPaused;
        public event Action? PlaybackResumed;
        public event Action<Exception>? PlaybackError;
        public event Action<TimeSpan, TimeSpan>? PlaybackTimeChanged;

        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public TimeSpan CurrentPosition { get; private set; }
        public TimeSpan TotalDuration { get; private set; }

        [Obsolete("Constructor is deprecated")]
        public AudioPacketReader(string filePath)
        {
            throw new NotSupportedException(
                "AudioPacketReader has been replaced by FilePacketSource + FilePlaybackPipeline. " +
                "Please update your code to use the new architecture.");
        }

        [Obsolete("Method is deprecated")]
        public void SetMasterVolume(float volume)
        {
            throw new NotSupportedException("Use FilePlaybackPipeline.SetMasterVolume() instead.");
        }

        [Obsolete("Method is deprecated")]
        public TimeSpan CalculateTotalDuration()
        {
            throw new NotSupportedException("Use FilePacketSource.TotalDuration instead.");
        }

        [Obsolete("Method is deprecated")]
        public void StartPlayback()
        {
            throw new NotSupportedException("Use FilePlaybackPipeline.PlayAsync() instead.");
        }

        [Obsolete("Method is deprecated")]
        public void PausePlayback()
        {
            throw new NotSupportedException("Use FilePlaybackPipeline.Pause() instead.");
        }

        [Obsolete("Method is deprecated")]
        public void StopPlayback()
        {
            throw new NotSupportedException("Use FilePlaybackPipeline.StopAsync() instead.");
        }

        [Obsolete("Method is deprecated")]
        public void SeekTo(TimeSpan position)
        {
            throw new NotSupportedException("Use FilePlaybackPipeline.SeekAsync() instead.");
        }

        [Obsolete("Method is deprecated")]
        public List<PlayerFrequencyInfo> GetAllFrequencyModulations()
        {
            throw new NotSupportedException("Use FilePlaybackPipeline.GetAvailableFrequencies() instead.");
        }

        [Obsolete("Method is deprecated")]
        public void SetFrequencyFilter(List<PlayerFrequencyInfo> frequencies)
        {
            throw new NotSupportedException("Use FilePlaybackPipeline.SetFrequencyGate() instead.");
        }

        [Obsolete("Method is deprecated")]
        public void ClearFrequencyFilter()
        {
            throw new NotSupportedException("Use FilePlaybackPipeline.SetFrequencyGate() instead.");
        }

        [Obsolete("Method is deprecated")]
        public List<AudioPacketMetadata> GetFilteredPackets()
        {
            throw new NotSupportedException("Use FilePacketSource streaming methods instead.");
        }

        [Obsolete("Method is deprecated")]
        public Task PreLoadPacketCacheAsync()
        {
            throw new NotSupportedException("FilePacketSource uses memory-mapped files, no caching needed.");
        }

        [Obsolete("Method is deprecated")]
        public void SetPilotFilterMode(string pilotId, double frequency, AeroDebrief.Core.Audio.PilotFilterMode mode)
        {
            throw new NotSupportedException("Use FilePlaybackPipeline with PilotFilter instead.");
        }

        public void Dispose()
        {
            // Stub - nothing to dispose
        }
    }
}
