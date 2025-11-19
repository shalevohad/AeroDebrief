using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Opus.Core;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using NLog;
using AeroDebrief.Core.Models;
using AeroDebrief.Core.Helpers;
using System;
using System.Linq;
using AeroDebrief.Core.Interfaces.Audio;

namespace AeroDebrief.Core.Audio
{
    /// <summary>Handles audio processing with SRS Common integration</summary>
    public sealed class AudioProcessingEngine : IAudioProcessingEngine
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly Dictionary<string, Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Opus.Core.OpusDecoder> _opusDecoders = new();
        private readonly Dictionary<string, float> _transmitterVolumes = new();
        private readonly Dictionary<string, float[]?> _transmitterLastPacket = new(); // NEW: Track last packet per transmitter for crossfading
        private float _masterVolume = 1.0f;
        private bool _disposed;
        
        // NEW: Crossfade settings for smooth packet transitions
        private const int CrossfadeSamples = 240; // 5ms crossfade at 48kHz (reduced from 10ms for tighter feel)
        private const double MaxGapForCrossfade = 0.1; // 100ms - apply crossfade if gap is smaller

        public void Initialize()
        {
            Logger.Info("Audio processing engine initialized with crossfade support");
        }

        public void SetMasterVolume(float volume)
        {
            _masterVolume = Math.Clamp(volume, 0.0f, Constants.MAX_VOLUME);
            Logger.Info($"?? AudioProcessingEngine: Master volume set to {_masterVolume:F2}");
        }

        /// <summary>
        /// Decode packet and resample to internal output sample rate. Does NOT apply volume/effects.
        /// </summary>
        public float[] DecodePacketToFloat(AudioPacketMetadata packet)
        {
            try
            {
                if (packet.AudioPayload == null || packet.AudioPayload.Length == 0)
                {
                    Logger.Debug($"Empty payload for packet from {packet.TransmitterGuid}");
                    return new float[Constants.OPUS_FRAME_SIZE];
                }

                var isOpus = Helpers.Helpers.IsOpusEncoded(packet);
                Logger.Debug($"?? DecodePacketToFloat: Packet from {packet.TransmitterGuid}, size={packet.AudioPayload.Length} bytes, isOpus={isOpus}");

                float[] audioData;
                if (isOpus)
                {
                    audioData = DecodeOpusAudio(packet);
                }
                else
                {
                    audioData = Helpers.Helpers.ConvertPcm16ToFloat(packet.AudioPayload);
                }

                if (audioData == null || audioData.Length == 0)
                {
                    Logger.Warn($"?? No audio data after decoding packet from {packet.TransmitterGuid}");
                    return new float[Constants.OPUS_FRAME_SIZE];
                }

                // CRITICAL DIAGNOSTIC: Check amplitude IMMEDIATELY after decoding
                var maxAmplitudeAfterDecode = audioData.Max(Math.Abs);
                var nonZeroSamples = audioData.Count(s => Math.Abs(s) > 0.001f);
                Logger.Info($"?? AFTER DECODE: max amplitude={maxAmplitudeAfterDecode:F4}, non-zero samples={nonZeroSamples}/{audioData.Length}, sampleRate={packet.SampleRate}");

                if (maxAmplitudeAfterDecode == 0)
                {
                    Logger.Error($"?? CRITICAL: Decoded audio is SILENT! This should never happen for valid audio data. Payload size: {packet.AudioPayload.Length} bytes");
                    return audioData; // Return silence, don't try to process
                }

                // CRITICAL FIX: Normalize audio if amplitude is too low
                // Opus decoder sometimes returns very quiet audio that needs amplification
                if (maxAmplitudeAfterDecode > 0 && maxAmplitudeAfterDecode < 0.1f)
                {
                    // Audio is too quiet - normalize it to use more of the dynamic range
                    // Target peak around 0.5 (50% of full scale) to leave headroom for mixing
                    // Reduced from 0.7 to prevent clipping when multiple frequencies are mixed
                    const float targetPeak = 0.5f;
                    float amplificationFactor = targetPeak / maxAmplitudeAfterDecode;
                    
                    Logger.Info($"? NORMALIZING: Audio too quiet ({maxAmplitudeAfterDecode:F4}), amplifying by {amplificationFactor:F2}x to reach {targetPeak:F2} peak");
                    
                    // Apply amplification WITHOUT hard clipping
                    // This prevents the 25% clipping issue seen in tests
                    for (int i = 0; i < audioData.Length; i++)
                    {
                        audioData[i] *= amplificationFactor;
                        
                        // Soft limiter: use tanh for smooth limiting instead of hard clamp
                        // This prevents harsh clipping distortion while still keeping samples in range
                        if (Math.Abs(audioData[i]) > 0.95f)
                        {
                            audioData[i] = (float)Math.Tanh(audioData[i] * 1.1) * 0.95f;
                        }
                    }
                    
                    var maxAfterNormalization = audioData.Max(Math.Abs);
                    Logger.Info($"? AFTER NORMALIZATION: max amplitude={maxAfterNormalization:F4} (target was {targetPeak:F2})");
                }

                // Resample if needed (ensure output sample rate)
                if (packet.SampleRate != Constants.OUTPUT_SAMPLE_RATE)
                {
                    audioData = Helpers.Helpers.ResampleAudio(audioData, packet.SampleRate, Constants.OUTPUT_SAMPLE_RATE);
                    var maxAmplitudeAfterResample = audioData.Max(Math.Abs);
                    Logger.Debug($"?? AFTER RESAMPLE: {packet.SampleRate}Hz -> {Constants.OUTPUT_SAMPLE_RATE}Hz, max amplitude={maxAmplitudeAfterResample:F4}");
                }

                return audioData;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error decoding packet from {packet.TransmitterGuid}");
                return new float[Constants.OPUS_FRAME_SIZE];
            }
        }

        public float[] ProcessPacket(AudioPacketMetadata packet)
        {
            try
            {
                // Ensure transmitter has volume set (default 1.0 if not set)
                if (!_transmitterVolumes.ContainsKey(packet.TransmitterGuid))
                {
                    _transmitterVolumes[packet.TransmitterGuid] = 1.0f;
                }

                // Decode and resample to internal format
                var audioData = DecodePacketToFloat(packet);

                if (audioData == null || audioData.Length == 0)
                {
                    Logger.Debug($"No audio data after decoding packet from {packet.TransmitterGuid}");
                    return new float[Constants.OPUS_FRAME_SIZE];
                }

                // NEW: Apply crossfade to eliminate jittering/clicking between packets
                audioData = ApplyCrossfade(packet.TransmitterGuid, audioData, packet.Timestamp);

                // CRITICAL DIAGNOSTIC: Check amplitude before volume control
                var maxAmplitudeBeforeVolume = audioData.Max(Math.Abs);
                //Logger.Debug($"BEFORE VOLUME: max amplitude={maxAmplitudeBeforeVolume:F4}");

                // Apply volume control
                var effectiveVolume = GetEffectiveVolume(packet.TransmitterGuid);
                //Logger.Info($"VOLUME: effective={effectiveVolume:F4}, master={_masterVolume:F4}, transmitter={_transmitterVolumes.GetValueOrDefault(packet.TransmitterGuid, 1.0f):F4}");
                
                if (effectiveVolume > 0)
                {
                    ApplyVolumeControl(audioData, effectiveVolume);
                }
                else
                {
                    Logger.Warn($"CRITICAL: Effective volume is {effectiveVolume:F4} - audio will be SILENT!");
                }

                // CRITICAL DIAGNOSTIC: Check amplitude after volume control
                var maxAmplitudeAfterVolume = audioData.Max(Math.Abs);
                //Logger.Info($"AFTER VOLUME: max amplitude={maxAmplitudeAfterVolume:F4}");

                if (maxAmplitudeAfterVolume == 0 && maxAmplitudeBeforeVolume > 0)
                {
                    Logger.Error($"CRITICAL: Volume control made audio SILENT! Before: {maxAmplitudeBeforeVolume:F4}, After: {maxAmplitudeAfterVolume:F4}, Volume: {effectiveVolume:F4}");
                }

                // Apply basic audio effects (disabled for now)
                ApplyBasicAudioEffects(audioData, packet);

                return audioData;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error processing packet from {packet.TransmitterGuid}");
                // Return silence on error to prevent audio pipeline from breaking
                return new float[Constants.OPUS_FRAME_SIZE];
            }
        }

        /// <summary>
        /// NEW: Applies crossfade to smooth transitions between packets from the same transmitter
        /// This eliminates clicks, pops, and jittering during real-time playback
        /// </summary>
        private float[] ApplyCrossfade(string transmitterGuid, float[] currentAudio, DateTime currentTimestamp)
        {
            if (currentAudio == null || currentAudio.Length == 0)
                return currentAudio;

            // Initialize tracking for new transmitter
            if (!_transmitterLastPacket.ContainsKey(transmitterGuid))
            {
                _transmitterLastPacket[transmitterGuid] = null;
            }

            var lastPacket = _transmitterLastPacket[transmitterGuid];
            
            // If this is first packet or last packet was null, apply fade-in only
            if (lastPacket == null || lastPacket.Length == 0)
            {
                ApplyFadeIn(currentAudio, CrossfadeSamples);
                _transmitterLastPacket[transmitterGuid] = currentAudio;
                Logger.Trace($"Crossfade: Applied fade-in for first packet from {transmitterGuid}");
                return currentAudio;
            }

            // Apply crossfade between last packet end and current packet start
            // This creates smooth transitions and eliminates clicks
            ApplyCrossfadeBetweenPackets(lastPacket, currentAudio, CrossfadeSamples);
            
            // Store current packet for next crossfade
            _transmitterLastPacket[transmitterGuid] = currentAudio;
            
            return currentAudio;
        }

        /// <summary>
        /// Applies fade-in envelope at the start of audio
        /// </summary>
        private void ApplyFadeIn(float[] audio, int fadeLength)
        {
            fadeLength = Math.Min(fadeLength, audio.Length);
            
            for (int i = 0; i < fadeLength; i++)
            {
                float fadeEnvelope = (float)i / fadeLength;
                audio[i] *= fadeEnvelope;
            }
        }

        /// <summary>
        /// Applies fade-out envelope at the end of audio
        /// </summary>
        private void ApplyFadeOut(float[] audio, int fadeLength)
        {
            fadeLength = Math.Min(fadeLength, audio.Length);
            int startIdx = audio.Length - fadeLength;
            
            for (int i = 0; i < fadeLength; i++)
            {
                float fadeEnvelope = 1.0f - ((float)i / fadeLength);
                audio[startIdx + i] *= fadeEnvelope;
            }
        }

        /// <summary>
        /// Applies crossfade between the end of previous packet and start of current packet
        /// </summary>
        private void ApplyCrossfadeBetweenPackets(float[] previousAudio, float[] currentAudio, int fadeLength)
        {
            if (previousAudio == null || currentAudio == null)
                return;

            fadeLength = Math.Min(fadeLength, Math.Min(previousAudio.Length, currentAudio.Length));
            
            // Fade out last samples of previous audio
            ApplyFadeOut(previousAudio, fadeLength);
            
            // Fade in first samples of current audio
            ApplyFadeIn(currentAudio, fadeLength);
            
            Logger.Trace($"Crossfade: Applied {fadeLength} sample crossfade between packets");
        }

        public void ResetDecoders()
        {
            // Clear all decoders - they will be recreated on next use
            foreach (var decoder in _opusDecoders.Values)
            {
                try
                {
                    decoder?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to dispose OPUS decoder");
                }
            }
            
            _opusDecoders.Clear();
            
            // NEW: Clear crossfade history on seek to prevent artifacts
            _transmitterLastPacket.Clear();
            
            Logger.Debug("All OPUS decoders cleared for seeking, crossfade history cleared");
        }

        private float[] DecodeOpusAudio(AudioPacketMetadata packet)
        {
            try
            {
                var decoder = GetOrCreateOpusDecoder(packet.TransmitterGuid);
                const int expectedSamples = Constants.OUTPUT_SAMPLE_RATE * Constants.OPUS_FRAME_DURATION_MS / 1000;
                
                // Use SRS Common OpusDecoder.DecodeFloat method
                var buffer = new float[expectedSamples];
                int samplesDecoded = decoder.DecodeFloat(packet.AudioPayload, buffer, false);

                if (samplesDecoded > 0)
                {
                    if (samplesDecoded < buffer.Length)
                    {
                        Array.Resize(ref buffer, samplesDecoded);
                    }
                    
                    return buffer;
                }
                else
                {
                    Logger.Debug($"OPUS decoder returned {samplesDecoded} samples for {packet.TransmitterGuid}");
                    return new float[expectedSamples];
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to decode OPUS audio for {packet.TransmitterGuid}");
                // Return silence on decode error
                return new float[Constants.OPUS_FRAME_SIZE];
            }
        }

        private Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Opus.Core.OpusDecoder GetOrCreateOpusDecoder(string transmitterGuid)
        {
            if (!_opusDecoders.TryGetValue(transmitterGuid, out var decoder))
            {
                // Use SRS Common OpusDecoder.Create factory method
                decoder = Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Opus.Core.OpusDecoder.Create(
                    Constants.OUTPUT_SAMPLE_RATE, 1);
                _opusDecoders[transmitterGuid] = decoder;
                Logger.Debug($"Created OPUS decoder for {transmitterGuid}");
            }
            return decoder;
        }

        private float GetTransmitterVolume(string transmitterGuid)
        {
            return _transmitterVolumes.TryGetValue(transmitterGuid, out var volume) ? volume : 1.0f;
        }

        private float GetEffectiveVolume(string transmitterGuid)
        {
            var transmitterVol = GetTransmitterVolume(transmitterGuid);
            var effective = transmitterVol * _masterVolume;
            
            // CRITICAL DIAGNOSTIC: Log if we're falling back to 1.0
            if (effective <= 0.0f && _masterVolume > 0.0f && transmitterVol >= 0.0f)
            {
                Logger.Debug($"?? Volume calculation resulted in 0.0 (transmitter={transmitterVol:F4}, master={_masterVolume:F4}) - using fallback 1.0");
                effective = 1.0f;
            }
            
            // CRITICAL DIAGNOSTIC: Log if master volume is 0
            if (_masterVolume <= 0.0f)
            {
                Logger.Warn($"?? Master volume is {_masterVolume:F4} - audio will be silent!");
            }
            
            // Log effective volume for debugging (only if non-default)
            if (Math.Abs(effective - 1.0f) > 0.001f || _masterVolume != 1.0f)
            {
                Logger.Trace($"GetEffectiveVolume: transmitter={transmitterVol:F4}, master={_masterVolume:F4}, effective={effective:F4}");
            }
            
            return effective;
        }

        private void ApplyVolumeControl(float[] audioBuffer, float volume)
        {
            // CRITICAL DIAGNOSTIC: Log actual volume being applied
            if (Math.Abs(volume - 1.0f) > 0.001f)
            {
                Logger.Debug($"? ApplyVolumeControl: Applying volume {volume:F4} (master={_masterVolume:F4})");
            }
            
            if (Math.Abs(volume - 1.0f) < 0.001f) 
            {
                // Volume is 1.0, no need to apply
                return;
            }

            var originalMax = audioBuffer.Length > 0 ? audioBuffer.Max(Math.Abs) : 0f;
            
            for (int i = 0; i < audioBuffer.Length; i++)
            {
                audioBuffer[i] = Math.Clamp(audioBuffer[i] * volume, -1.0f, 1.0f);
            }
            
            var newMax = audioBuffer.Length > 0 ? audioBuffer.Max(Math.Abs) : 0f;
            
            // Only log if volume actually changed the amplitude
            if (originalMax > 0)
            {
                Logger.Trace($"Volume control applied: {volume:F2}, amplitude {originalMax:F4} -> {newMax:F4}");
            }
        }

        private void ApplyBasicAudioEffects(float[] audioData, AudioPacketMetadata packet)
        {
            // Audio effects disabled for Opus-decoded voice to maintain clarity
            return;
            
            /* DISABLED - These effects were causing "fax machine" sounds
            try
            {
                // Apply basic effects based on modulation type
                var modulation = (Modulation)packet.Modulation;
                
                switch (modulation)
                {
                    case Modulation.AM:
                        ApplyAmEffect(audioData);
                        break;
                    case Modulation.FM:
                        ApplyFmEffect(audioData);
                        break;
                    case Modulation.INTERCOM:
                        // Intercom is usually cleaner, apply minimal processing
                        break;
                    case Modulation.DISABLED:
                        // No effects for disabled modulation
                        break;
                    default:
                        // Unknown modulation, apply minimal AM-like effect
                        ApplyAmEffect(audioData);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to apply audio effects");
                // Continue without effects on error
            }
            */
        }

        private void ApplyAmEffect(float[] audioData)
        {
            // Simple AM radio effect - add some high-frequency roll-off and slight distortion
            for (int i = 0; i < audioData.Length; i++)
            {
                // Simple high-frequency attenuation
                if (i > 0)
                {
                    audioData[i] = audioData[i] * 0.85f + audioData[i - 1] * 0.15f;
                }
                
                // Slight compression/saturation for radio effect
                var sample = audioData[i];
                audioData[i] = sample > 0 ? (float)Math.Tanh(sample * 1.2) : (float)Math.Tanh(sample * 1.2);
            }
        }

        private void ApplyFmEffect(float[] audioData)
        {
            // Simple FM radio effect - cleaner than AM but still some processing
            for (int i = 0; i < audioData.Length; i++)
            {
                // Lighter processing for FM
                if (i > 0)
                {
                    audioData[i] = audioData[i] * 0.92f + audioData[i - 1] * 0.08f;
                }
                
                // Very light compression
                var sample = audioData[i];
                audioData[i] = (float)Math.Tanh(sample * 1.05);
            }
        }

        public void SetTransmitterVolume(string transmitterGuid, float volume)
        {
            _transmitterVolumes[transmitterGuid] = Math.Clamp(volume, 0.0f, Constants.MAX_VOLUME);
            Logger.Debug($"Set volume for {transmitterGuid} to {volume:F2}");
        }

        public void Dispose()
        {
            if (_disposed) return;

            foreach (var decoder in _opusDecoders.Values)
            {
                try
                {
                    decoder?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Error disposing OPUS decoder");
                }
            }
            _opusDecoders.Clear();

            _disposed = true;
            Logger.Debug("AudioProcessingEngine disposed");
        }
    }
}