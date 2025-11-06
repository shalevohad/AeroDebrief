using AeroDebrief.Core.Audio;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AeroDebrief.Tests.Audio
{
    /// <summary>
    /// Mock implementation of IAudioOutputEngine for testing.
    /// Records all operations for verification without requiring actual audio hardware.
    /// </summary>
    public class MockAudioOutputEngine : IAudioOutputEngine
    {
        public List<byte[]> WrittenFrames { get; } = new();
        public int InitializeCallCount { get; private set; }
        public int StartCallCount { get; private set; }
        public int StopCallCount { get; private set; }
        public int ClearBufferCallCount { get; private set; }
        public bool IsInitialized { get; private set; }
        public bool IsRunning { get; private set; }
        public float CurrentVolume { get; private set; } = 1.0f;
        public int TotalBytesWritten => WrittenFrames.Sum(f => f.Length);
        public bool IsDisposed { get; private set; }

        public Task InitializeAsync()
        {
            InitializeCallCount++;
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public void Start()
        {
            StartCallCount++;
            IsRunning = true;
        }

        public void Stop()
        {
            StopCallCount++;
            IsRunning = false;
        }

        public void SetMasterVolume(float volume)
        {
            CurrentVolume = Math.Clamp(volume, 0.0f, 2.0f);
        }

        public float GetMasterVolume()
        {
            return CurrentVolume;
        }

        public void ClearBuffer()
        {
            ClearBufferCallCount++;
            WrittenFrames.Clear();
        }

        public Task WriteAudioAsync(byte[] audioData)
        {
            if (audioData != null)
            {
                WrittenFrames.Add(audioData);
            }
            return Task.CompletedTask;
        }

        public Task WriteAudioAsync(byte[] audioData, bool isSilence, TimeSpan chunkEndTime = default, Action<TimeSpan>? positionUpdater = null)
        {
            return WriteAudioAsync(audioData);
        }

        public void Dispose()
        {
            IsDisposed = true;
            WrittenFrames.Clear();
        }
    }
}
