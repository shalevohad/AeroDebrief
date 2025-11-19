using System;
using System.Threading.Tasks;

namespace AeroDebrief.Core.Interfaces.Audio
{
    /// <summary>
    /// Audio output engine contract for rendering decoded audio to speakers.
    /// Abstracts the underlying audio API (WASAPI, DirectSound, test implementations).
    /// </summary>
    public interface IAudioOutputEngine : IDisposable
    {
        Task InitializeAsync();
        void Start();
        void Stop();
        void SetMasterVolume(float volume);
        float GetMasterVolume();
        void ClearBuffer();
        Task WriteAudioAsync(byte[] audioData);
        Task WriteAudioAsync(byte[] audioData, bool isSilence, TimeSpan chunkEndTime = default, Action<TimeSpan>? positionUpdater = null, AudioPacketMetadata? packet = null);
    }
}
