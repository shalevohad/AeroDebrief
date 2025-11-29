using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.Storage.Abstractions;

namespace AeroDebrief.Core.Interfaces.Storage
{
    /// <summary>
    /// Repository contract for frequency statistics and analysis.
    /// Abstracts underlying storage technology.
    /// 
    /// Responsibilities:
    /// - Track unique frequencies in recording
    /// - Aggregate usage statistics (packet count, duration)
    /// - Filter by coalition, player, activity level
    /// - Rebuild statistics during live recording
    /// 
    /// Used by:
    /// - DatabasePacketSource for frequency filtering
    /// - FrequencyManager for UI frequency selection
    /// - Analysis services for frequency discovery
    /// </summary>
    public interface IFrequencyRepository : IDisposable
    {
        /// <summary>
        /// Get all unique frequencies in the recording
        /// </summary>
        Task<List<FrequencyInfo>> GetAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Get frequency info for a specific frequency
        /// </summary>
        Task<FrequencyInfo?> GetByFrequencyAsync(double frequency, byte modulation, CancellationToken ct = default);

        /// <summary>
        /// Get most active frequencies (by packet count)
        /// </summary>
        Task<List<FrequencyInfo>> GetMostActiveAsync(int limit = 10, CancellationToken ct = default);

        /// <summary>
        /// Get frequencies used by a specific player
        /// </summary>
        Task<List<FrequencyInfo>> GetByPlayerAsync(string playerName, CancellationToken ct = default);

        /// <summary>
        /// Get frequencies for a specific coalition
        /// </summary>
        Task<List<FrequencyInfo>> GetByCoalitionAsync(int coalition, CancellationToken ct = default);

        /// <summary>
        /// Rebuild frequency statistics (call periodically during live recording)
        /// </summary>
        Task RebuildStatsAsync(CancellationToken ct = default);

        /// <summary>
        /// Get total number of unique frequencies
        /// </summary>
        Task<int> GetCountAsync(CancellationToken ct = default);
    }
}
