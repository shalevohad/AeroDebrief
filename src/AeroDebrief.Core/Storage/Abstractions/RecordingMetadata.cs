using System;

namespace AeroDebrief.Core.Storage.Abstractions
{
    /// <summary>
    /// Recording metadata data model.
    /// Contains information about the recording session (server, timing).
    /// </summary>
    public class RecordingMetadata
    {
        public required string Version { get; set; }
        public required string ServerIp { get; set; }
        public required int ServerPort { get; set; }
        public required DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    /// <summary>
    /// Recording statistics data model.
    /// Contains aggregated statistics about the recording (packet count, duration).
    /// </summary>
    public class RecordingStats
    {
        public long TotalPackets { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsLive { get; set; }
        public DateTime? LastUpdate { get; set; }
        
        public double PacketsPerSecond => Duration.TotalSeconds > 0 
            ? TotalPackets / Duration.TotalSeconds 
            : 0;
    }
}
