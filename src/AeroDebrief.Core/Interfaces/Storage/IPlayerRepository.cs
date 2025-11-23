using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.Storage.Abstractions;

namespace AeroDebrief.Core.Interfaces.Storage
{
    /// <summary>
    /// Repository contract for player/user statistics and analysis.
    /// Abstracts underlying storage technology.
    /// 
    /// Responsibilities:
    /// - Track unique players in recording
    /// - Aggregate transmission statistics
    /// - Filter by coalition, aircraft type, frequency
    /// - Rebuild statistics during live recording
    /// 
    /// Used by:
    /// - DatabasePacketSource for player filtering
    /// - UI for player selection and statistics
    /// - Analysis services for player activity tracking
    /// </summary>
    public interface IPlayerRepository : IDisposable
    {
        /// <summary>
        /// Get all unique players in the recording
        /// </summary>
        Task<List<PlayerStats>> GetAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Get player info by name
        /// </summary>
        Task<PlayerStats?> GetByNameAsync(string playerName, CancellationToken ct = default);

        /// <summary>
        /// Get most active players (by transmission count)
        /// </summary>
        Task<List<PlayerStats>> GetMostActiveAsync(int limit = 10, CancellationToken ct = default);

        /// <summary>
        /// Get players by coalition
        /// </summary>
        Task<List<PlayerStats>> GetByCoalitionAsync(int coalition, CancellationToken ct = default);

        /// <summary>
        /// Get players using a specific frequency
        /// </summary>
        Task<List<PlayerStats>> GetByFrequencyAsync(double frequency, CancellationToken ct = default);

        /// <summary>
        /// Get players flying a specific aircraft type
        /// </summary>
        Task<List<PlayerStats>> GetByAircraftTypeAsync(string unitType, CancellationToken ct = default);

        /// <summary>
        /// Rebuild player statistics (call periodically during live recording)
        /// </summary>
        Task RebuildStatsAsync(CancellationToken ct = default);

        /// <summary>
        /// Get total number of unique players
        /// </summary>
        Task<int> GetCountAsync(CancellationToken ct = default);
    }
}
