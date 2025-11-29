using AeroDebrief.Core.Models;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace AeroDebrief.Core.Audio
{
    /// <summary>
    /// Caches decoded amplitude data to avoid re-processing packets.
    /// Implements a query-based architecture for efficient data access.
    /// Thread-safe for concurrent access from multiple graph components.
    /// </summary>
    public sealed class AmplitudeDataCache : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        // Cache key: (frequency, transmitterGuid, packetTimestamp)
        private readonly ConcurrentDictionary<(double, string, DateTime), CachedAmplitudeData> _cache = new();
        
        // Spatial index for efficient time-range queries
        private readonly ConcurrentDictionary<(double frequency, string transmitter), SortedList<DateTime, CachedAmplitudeData>> _timeIndex = new();
        
        private long _totalCacheHits;
        private long _totalCacheMisses;
        private bool _disposed;

        /// <summary>
        /// Try to get cached amplitude data for a specific packet.
        /// </summary>
        public bool TryGetAmplitudeData(double frequency, string transmitterGuid, DateTime timestamp, out CachedAmplitudeData? data)
        {
            var key = (frequency, transmitterGuid, timestamp);
            
            if (_cache.TryGetValue(key, out var cachedData))
            {
                System.Threading.Interlocked.Increment(ref _totalCacheHits);
                data = cachedData;
                return true;
            }
            
            System.Threading.Interlocked.Increment(ref _totalCacheMisses);
            data = null;
            return false;
        }
        
        /// <summary>
        /// Store amplitude data in cache for future queries.
        /// </summary>
        public void StoreAmplitudeData(double frequency, string transmitterGuid, DateTime timestamp, float[] decodedSamples, float[] amplitudeEnvelope)
        {
            var key = (frequency, transmitterGuid, timestamp);
            var data = new CachedAmplitudeData
            {
                Frequency = frequency,
                TransmitterGuid = transmitterGuid,
                Timestamp = timestamp,
                DecodedSamples = decodedSamples,
                AmplitudeEnvelope = amplitudeEnvelope,
                CachedAt = DateTime.UtcNow
            };
            
            _cache.TryAdd(key, data);
            
            // Update time index for efficient range queries
            var indexKey = (frequency, transmitterGuid);
            var timeIndex = _timeIndex.GetOrAdd(indexKey, _ => new SortedList<DateTime, CachedAmplitudeData>());
            
            lock (timeIndex)
            {
                timeIndex[timestamp] = data;
            }
        }
        
        /// <summary>
        /// Query amplitude data for a specific frequency/transmitter within a time range.
        /// This is MUCH faster than iterating through all packets.
        /// </summary>
        public IEnumerable<CachedAmplitudeData> QueryTimeRange(double frequency, string transmitterGuid, DateTime startTime, DateTime endTime)
        {
            var indexKey = (frequency, transmitterGuid);
            
            if (!_timeIndex.TryGetValue(indexKey, out var timeIndex))
            {
                yield break; // No data for this frequency/transmitter
            }
            
            // Binary search for efficient range query
            lock (timeIndex)
            {
                // Find the start index using binary search
                int startIndex = FindStartIndex(timeIndex, startTime);
                
                if (startIndex < 0 || startIndex >= timeIndex.Count)
                {
                    yield break;
                }
                
                // Iterate forward until we exceed the end time
                for (int i = startIndex; i < timeIndex.Count; i++)
                {
                    var kvp = timeIndex.ElementAt(i);
                    
                    if (kvp.Key > endTime)
                    {
                        break; // We've passed the end time
                    }
                    
                    if (kvp.Key >= startTime)
                    {
                        yield return kvp.Value;
                    }
                }
            }
        }
        
        /// <summary>
        /// Find the starting index for a time range query using binary search.
        /// </summary>
        private int FindStartIndex(SortedList<DateTime, CachedAmplitudeData> timeIndex, DateTime startTime)
        {
            if (timeIndex.Count == 0)
            {
                return -1;
            }
            
            // Binary search to find the first element >= startTime
            int left = 0;
            int right = timeIndex.Count - 1;
            int result = 0;
            
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                var midTime = timeIndex.Keys[mid];
                
                if (midTime < startTime)
                {
                    left = mid + 1;
                }
                else
                {
                    result = mid;
                    right = mid - 1;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Get all frequencies currently in the cache.
        /// </summary>
        public IEnumerable<(double frequency, string transmitter)> GetCachedFrequencies()
        {
            return _timeIndex.Keys;
        }
        
        /// <summary>
        /// Get cache statistics for monitoring and diagnostics.
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics
            {
                TotalEntries = _cache.Count,
                TotalCacheHits = _totalCacheHits,
                TotalCacheMisses = _totalCacheMisses,
                HitRate = _totalCacheHits + _totalCacheMisses > 0 
                    ? (double)_totalCacheHits / (_totalCacheHits + _totalCacheMisses) 
                    : 0.0,
                FrequencyCount = _timeIndex.Count
            };
        }
        
        /// <summary>
        /// Clear the cache to free memory.
        /// </summary>
        public void Clear()
        {
            Logger.Info($"Clearing amplitude cache. Statistics: {GetStatistics()}");
            
            _cache.Clear();
            _timeIndex.Clear();
            _totalCacheHits = 0;
            _totalCacheMisses = 0;
        }
        
        public void Dispose()
        {
            if (_disposed)
                return;
            
            var stats = GetStatistics();
            Logger.Info($"AmplitudeDataCache disposing. Final statistics: Entries={stats.TotalEntries}, Hits={stats.TotalCacheHits}, Misses={stats.TotalCacheMisses}, HitRate={stats.HitRate:P2}");
            
            Clear();
            _disposed = true;
        }
    }
    
    /// <summary>
    /// Represents cached amplitude data for a single packet.
    /// </summary>
    public class CachedAmplitudeData
    {
        public double Frequency { get; set; }
        public string TransmitterGuid { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public float[] DecodedSamples { get; set; } = Array.Empty<float>();
        public float[] AmplitudeEnvelope { get; set; } = Array.Empty<float>();
        public DateTime CachedAt { get; set; }
    }
    
    /// <summary>
    /// Cache performance statistics.
    /// </summary>
    public class CacheStatistics
    {
        public int TotalEntries { get; set; }
        public long TotalCacheHits { get; set; }
        public long TotalCacheMisses { get; set; }
        public double HitRate { get; set; }
        public int FrequencyCount { get; set; }
        
        public override string ToString()
        {
            return $"Entries={TotalEntries}, Hits={TotalCacheHits}, Misses={TotalCacheMisses}, HitRate={HitRate:P2}, Frequencies={FrequencyCount}";
        }
    }
}
