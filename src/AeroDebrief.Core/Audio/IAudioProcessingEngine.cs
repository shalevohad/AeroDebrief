using System;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// Interface for audio processing engines
    /// </summary>
    public interface IAudioProcessingEngine : IDisposable
    {
        void Initialize();
        void SetMasterVolume(float volume);
        float[] DecodePacketToFloat(AudioPacketMetadata packet);
        float[] ProcessPacket(AudioPacketMetadata packet);
        void ResetDecoders();
        void SetTransmitterVolume(string transmitterGuid, float volume);
    }
}
