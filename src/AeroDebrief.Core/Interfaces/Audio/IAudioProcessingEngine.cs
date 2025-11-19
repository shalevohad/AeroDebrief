using System;

namespace AeroDebrief.Core.Interfaces.Audio
{
    /// <summary>
    /// Audio processing engine contract for decoding and processing audio packets.
    /// Handles Opus decoding, volume control, and per-transmitter audio processing.
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
