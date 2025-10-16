namespace AeroDebrief.Core.Helpers
{
    /// <summary>
    /// Helper methods for player information and metadata
    /// </summary>
    public static class PlayerHelpers
    {
        /// <summary>
        /// Gets a fallback player name from PlayerInfo and GUID
        /// </summary>
        /// <param name="playerInfo">Player information</param>
        /// <param name="transmitterGuid">Transmitter GUID as fallback</param>
        /// <returns>Display name for the player</returns>
        public static string GetPlayerNameWithFallback(PlayerInfo? playerInfo, string transmitterGuid)
        {
            if (playerInfo != null && !string.IsNullOrEmpty(playerInfo.Name) && playerInfo.Name != transmitterGuid)
                return playerInfo.Name;
            
            if (!string.IsNullOrEmpty(transmitterGuid))
                return $"Unknown Player ({StringHelpers.GetDisplayGuid(transmitterGuid)})";
            
            return "Unknown Player";
        }

        /// <summary>
        /// Gets seat information with fallback
        /// </summary>
        /// <param name="seat">Seat number</param>
        /// <returns>Formatted seat string</returns>
        public static string GetSeatWithFallback(int seat)
        {
            return seat >= 0 ? $"Seat {seat}" : "Unknown Seat";
        }

        /// <summary>
        /// Gets recording status display string
        /// </summary>
        /// <param name="allowRecord">Whether recording is allowed</param>
        /// <returns>Recording status string</returns>
        public static string GetRecordingStatus(bool allowRecord)
        {
            return allowRecord ? "Recording Allowed" : "Recording Denied";
        }
    }
}