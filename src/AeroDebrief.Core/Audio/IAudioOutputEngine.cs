using System;
using System.Threading.Tasks;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// Interface for audio output engines
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
        Task WriteAudioAsync(byte[] audioData, bool isSilence, TimeSpan chunkEndTime = default, Action<TimeSpan>? positionUpdater = null);
    }
}
