using System;
using System.Collections.Generic;

namespace AeroDebrief.UI.Models
{
    /// <summary>
    /// Represents a tile of amplitude data for a frequency/pilot combination.
    /// Phase 8: Core data structure for tile-based loading system.
    /// 
    /// Tiles are fixed-size chunks of time (typically 5 minutes) that can be
    /// loaded and unloaded independently to manage memory efficiently.
    /// </summary>
    public class SeriesTile
    {
        /// <summary>
        /// Frequency in Hz (e.g., 251000000.0 for UHF 251.0 MHz).
        /// </summary>
        public double Frequency { get; set; }
        
        /// <summary>
        /// Pilot identifier (e.g., "VIPER-1", "SNAKE-2").
        /// Empty string for frequency-level tiles.
        /// </summary>
        public string PilotId { get; set; } = string.Empty;
        
        /// <summary>
        /// Start time of this tile (inclusive).
        /// Aligned to tile boundaries (e.g., 10:00:00, 10:05:00, 10:10:00).
        /// </summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// End time of this tile (exclusive).
        /// Typically StartTime + TileSize (5 minutes).
        /// </summary>
        public DateTime EndTime { get; set; }
        
        /// <summary>
        /// Resolution/layer of this tile.
        /// Determines data point density and query performance.
        /// </summary>
        public Resolution Resolution { get; set; }
        
        /// <summary>
        /// Data points in this tile.
        /// Sorted by time in ascending order.
        /// </summary>
        public List<AmplitudePoint> Points { get; set; } = new();
        
        /// <summary>
        /// Number of points in this tile.
        /// </summary>
        public int PointCount => Points.Count;
        
        /// <summary>
        /// Estimated memory usage in bytes.
        /// Calculation: 16 bytes per AmplitudePoint (8 bytes DateTime + 8 bytes double).
        /// Actual memory may be higher due to List overhead and alignment.
        /// </summary>
        public long MemoryBytes => Points.Count * 16L;
        
        /// <summary>
        /// Unique key for this tile.
        /// Format: "freq_{frequency}_pilot_{pilotId}_{startTicks}_{resolution}"
        /// Used for caching and deduplication.
        /// </summary>
        public string Key => 
            $"freq_{Frequency:F0}_pilot_{PilotId}_{StartTime.Ticks}_{Resolution}";
        
        /// <summary>
        /// Duration of this tile.
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;
        
        /// <summary>
        /// Returns true if this tile contains data for the specified time.
        /// </summary>
        public bool ContainsTime(DateTime time)
        {
            return time >= StartTime && time < EndTime;
        }
        
        /// <summary>
        /// Returns true if this tile overlaps with the specified time range.
        /// </summary>
        public bool Overlaps(DateTime rangeStart, DateTime rangeEnd)
        {
            return StartTime < rangeEnd && EndTime > rangeStart;
        }
        
        public override string ToString()
        {
            return $"Tile[{Frequency / 1_000_000.0:F3}MHz/{PilotId}]: " +
                   $"{StartTime:HH:mm:ss}-{EndTime:HH:mm:ss} " +
                   $"({PointCount} pts, {Resolution})";
        }
    }
    
    /// <summary>
    /// Simple data point with time and amplitude.
    /// Designed for minimal memory footprint (16 bytes).
    /// </summary>
    public struct AmplitudePoint
    {
        /// <summary>
        /// Time of this data point.
        /// </summary>
        public DateTime Time { get; set; }
        
        /// <summary>
        /// Amplitude in dBFS (decibels relative to full scale).
        /// Typically ranges from -80 dBFS (silence) to -10 dBFS (loud).
        /// </summary>
        public double Amplitude { get; set; }
        
        public AmplitudePoint(DateTime time, double amplitude)
        {
            Time = time;
            Amplitude = amplitude;
        }
        
        public override string ToString()
        {
            return $"[{Time:HH:mm:ss.fff}: {Amplitude:F1} dBFS]";
        }
    }
    
    /// <summary>
    /// Resolution levels for multi-resolution tile system.
    /// Lower resolution = fewer points = faster queries = less detail.
    /// </summary>
    public enum Resolution
    {
        /// <summary>
        /// Highest detail: 10ms per point.
        /// Use when zoomed in very close (zoom ? 20x).
        /// ~6,000 points per minute.
        /// </summary>
        Layer0_10ms,
        
        /// <summary>
        /// High detail: 50ms per point.
        /// Use when zoomed in moderately (zoom 4x-20x).
        /// ~1,200 points per minute.
        /// </summary>
        Layer1_50ms,
        
        /// <summary>
        /// Medium detail: 250ms per point.
        /// Use when slightly zoomed (zoom 1.5x-4x).
        /// ~240 points per minute.
        /// </summary>
        Layer2_250ms,
        
        /// <summary>
        /// Overview: 1 second per point.
        /// Use for full recording view (zoom < 1.5x).
        /// ~60 points per minute.
        /// </summary>
        Layer3_1s
    }
}
