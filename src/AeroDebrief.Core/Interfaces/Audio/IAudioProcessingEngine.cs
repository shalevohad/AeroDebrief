using System;
using System.Collections.Generic;
using AeroDebrief.Core.Audio;
using AeroDebrief.Core.Models;

namespace AeroDebrief.Core.Interfaces.Audio
{
    /// <summary>
    /// Audio processing engine contract for decoding and processing audio packets.
    /// Handles Opus decoding, volume control, and per-transmitter audio processing.
    /// Supports caching for efficient query-based access (Phase 13: Performance Optimization).
    /// </summary>
    public interface IAudioProcessingEngine : IDisposable
    {
        void Initialize();
        void SetMasterVolume(float volume);
        float[] DecodePacketToFloat(AudioPacketMetadata packet);
        float[] ProcessPacket(AudioPacketMetadata packet);
        void ResetDecoders();
        void SetTransmitterVolume(string transmitterGuid, float volume);
        
        // Phase 13: Query-based architecture for performance
        void EnableAmplitudeCache();
        void DisableAmplitudeCache();
        CacheStatistics? GetCacheStatistics();
        float[] DecodePacketToFloatCached(AudioPacketMetadata packet);
        IEnumerable<CachedAmplitudeData> QueryAmplitudeData(double frequency, string transmitterGuid, DateTime startTime, DateTime endTime);
    }
}
