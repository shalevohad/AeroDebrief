using AeroDebrief.Core;
using AeroDebrief.Core.Audio;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace AeroDebrief.Tests.Audio
{
    /// <summary>
    /// Mock implementation of IAudioProcessingEngine for testing.
    /// Simulates audio processing without actual decoding.
    /// </summary>
    public class MockAudioProcessingEngine : IAudioProcessingEngine
    {
        private readonly ConcurrentDictionary<string, float> _transmitterVolumes = new();
        private float _masterVolume = 1.0f;
        public bool IsInitialized { get; private set; }
        public int ProcessedPacketCount { get; private set; }
        public bool IsDisposed { get; private set; }

        public void Initialize()
        {
            IsInitialized = true;
        }

        public void SetMasterVolume(float volume)
        {
            _masterVolume = Math.Clamp(volume, 0.0f, 2.0f);
        }

        public float[] DecodePacketToFloat(AudioPacketMetadata packet)
        {
            if (packet == null || packet.AudioPayload == null || packet.AudioPayload.Length == 0)
            {
                return new float[480]; // Return silence
            }

            // Simulate decoding: Convert byte[] to float[]
            var sampleCount = packet.AudioPayload.Length / 2; // 16-bit PCM
            var floatData = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                // Convert 16-bit PCM to float [-1.0, 1.0]
                short sample = (short)(packet.AudioPayload[i * 2] | (packet.AudioPayload[i * 2 + 1] << 8));
                floatData[i] = sample / 32768.0f;
            }

            return floatData;
        }

        public float[] ProcessPacket(AudioPacketMetadata packet)
        {
            ProcessedPacketCount++;

            if (packet == null)
            {
                return new float[480]; // Return silence for null packets
            }

            // Decode audio
            var audioData = DecodePacketToFloat(packet);

            // Apply volume control
            var effectiveVolume = GetEffectiveVolume(packet.TransmitterGuid);
            if (Math.Abs(effectiveVolume - 1.0f) > 0.001f)
            {
                for (int i = 0; i < audioData.Length; i++)
                {
                    audioData[i] = Math.Clamp(audioData[i] * effectiveVolume, -1.0f, 1.0f);
                }
            }

            return audioData;
        }

        public void ResetDecoders()
        {
            ProcessedPacketCount = 0;
            _transmitterVolumes.Clear();
        }

        public void SetTransmitterVolume(string transmitterGuid, float volume)
        {
            _transmitterVolumes[transmitterGuid] = Math.Clamp(volume, 0.0f, 2.0f);
        }

        public float GetTransmitterVolume(string transmitterGuid)
        {
            return _transmitterVolumes.TryGetValue(transmitterGuid, out var volume) ? volume : 1.0f;
        }

        private float GetEffectiveVolume(string transmitterGuid)
        {
            var transmitterVolume = GetTransmitterVolume(transmitterGuid);
            return transmitterVolume * _masterVolume;
        }

        public void Dispose()
        {
            IsDisposed = true;
            _transmitterVolumes.Clear();
        }
    }
}
