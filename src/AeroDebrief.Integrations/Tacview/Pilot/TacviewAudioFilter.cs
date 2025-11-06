using AeroDebrief.Core;
using AeroDebrief.Core.Audio;
using AeroDebrief.Integrations.Tacview.Models;
using AeroDebrief.Integrations.Tacview.Protocol.Messages;
using NLog;

namespace AeroDebrief.Integrations.Tacview.Pilot;

/// <summary>
/// Filters audio packets based on Tacview pilot selection and frequency configuration
/// Implements frequency-based filtering for selected and non-selected pilots
/// Implements IAudioPacketFilter and ISpatialAudioProvider for Core integration
/// </summary>
public class TacviewAudioFilter : IAudioPacketFilter, ISpatialAudioProvider
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private PilotSelectionMessage? _currentSelection;
    private int _packetsFiltered;
    private int _packetsAllowed;
    
    /// <summary>
    /// True if filter is active (Tacview connected with pilot selection)
    /// </summary>
    public bool IsActive => _currentSelection != null && _currentSelection.SelectedPilots.Count > 0;
    
    /// <summary>
    /// Number of packets filtered since last selection update
    /// </summary>
    public int PacketsFiltered => _packetsFiltered;
    
    /// <summary>
    /// Number of packets allowed since last selection update
    /// </summary>
    public int PacketsAllowed => _packetsAllowed;
    
    /// <summary>
    /// Fired when filter state changes (activated/deactivated)
    /// </summary>
    public event EventHandler<bool>? FilterStateChanged;
    
    /// <summary>
    /// Fired when pilot selection updates
    /// </summary>
    public event EventHandler<PilotSelectionMessage>? SelectionUpdated;
    
    /// <summary>
    /// Determines if an audio packet should be played based on pilot selection and frequency filtering
    /// </summary>
    /// <param name="packet">Audio packet to evaluate</param>
    /// <returns>True if packet should be played, false if filtered out</returns>
    public bool ShouldPlayPacket(AudioPacketMetadata packet)
    {
        if (packet == null)
            throw new ArgumentNullException(nameof(packet));
        
        // No filter active - play everything (default behavior when Tacview not connected)
        if (_currentSelection == null)
        {
            _packetsAllowed++;
            return true;
        }
        
        // Check if pilot is in selected list
        var selectedPilot = _currentSelection.SelectedPilots
            .FirstOrDefault(p => p.PilotId == packet.TransmitterGuid);
        
        if (selectedPilot != null)
        {
            // Selected pilot - check their enabled frequencies
            bool shouldPlay = ShouldPlayForSelectedPilot(selectedPilot, packet.Frequency);
            
            if (shouldPlay)
                _packetsAllowed++;
            else
                _packetsFiltered++;
            
            return shouldPlay;
        }
        else
        {
            // Non-selected pilot - check general enabled frequencies
            // Default: all DISABLED (empty list) - no audio from non-selected pilots
            bool shouldPlay = ShouldPlayForNonSelectedPilot(packet.Frequency);
            
            if (shouldPlay)
                _packetsAllowed++;
            else
                _packetsFiltered++;
            
            return shouldPlay;
        }
    }
    
    /// <summary>
    /// Determines if packet should play for a selected pilot based on their frequency configuration
    /// </summary>
    private bool ShouldPlayForSelectedPilot(PilotData pilot, double frequency)
    {
        // If no frequencies specified, play all (default: allow everything)
        if (pilot.EnabledFrequencies == null || pilot.EnabledFrequencies.Count == 0)
        {
            return true;
        }
        
        // Check if frequency is in enabled list (with tolerance for floating point comparison)
        const double FREQUENCY_TOLERANCE_HZ = 100.0; // 100 Hz tolerance
        
        return pilot.EnabledFrequencies.Any(enabledFreq =>
            Math.Abs(enabledFreq - frequency) < FREQUENCY_TOLERANCE_HZ);
    }
    
    /// <summary>
    /// Determines if packet should play for a non-selected pilot based on general frequency configuration
    /// </summary>
    private bool ShouldPlayForNonSelectedPilot(double frequency)
    {
        // Default behavior: all general frequencies DISABLED
        // Only play if frequency is explicitly in the enabled list
        
        if (_currentSelection?.GeneralEnabledFrequencies == null ||
            _currentSelection.GeneralEnabledFrequencies.Count == 0)
        {
            // Empty list = all disabled (default)
            return false;
        }
        
        // Check if frequency is in general enabled list
        const double FREQUENCY_TOLERANCE_HZ = 100.0;
        
        return _currentSelection.GeneralEnabledFrequencies.Any(enabledFreq =>
            Math.Abs(enabledFreq - frequency) < FREQUENCY_TOLERANCE_HZ);
    }
    
    /// <summary>
    /// Updates pilot selection from Tacview
    /// </summary>
    public void UpdateSelection(PilotSelectionMessage? selection)
    {
        var wasActive = IsActive;
        
        _currentSelection = selection;
        
        // Reset statistics on selection update
        _packetsFiltered = 0;
        _packetsAllowed = 0;
        
        if (selection != null)
        {
            Logger.Info($"Pilot selection updated: {selection.SelectedPilots.Count} pilots selected");
            
            foreach (var pilot in selection.SelectedPilots)
            {
                var freqCount = pilot.EnabledFrequencies?.Count ?? 0;
                Logger.Debug($"  - {pilot.PilotName} ({pilot.PilotId}): {freqCount} frequencies enabled, pan: {pilot.Pan:F2}");
            }
            
            var generalFreqCount = selection.GeneralEnabledFrequencies?.Count ?? 0;
            Logger.Debug($"  - General frequencies: {generalFreqCount} enabled (default: 0)");
        }
        else
        {
            Logger.Info("Pilot selection cleared (filter inactive)");
        }
        
        // Fire events
        SelectionUpdated?.Invoke(this, selection);
        
        if (IsActive != wasActive)
        {
            FilterStateChanged?.Invoke(this, IsActive);
        }
    }
    
    /// <summary>
    /// Clears current selection (disables filter)
    /// </summary>
    public void ClearSelection()
    {
        UpdateSelection(null);
    }
    
    /// <summary>
    /// Gets filtering statistics
    /// </summary>
    public FilterStatistics GetStatistics()
    {
        return new FilterStatistics
        {
            IsActive = IsActive,
            SelectedPilotCount = _currentSelection?.SelectedPilots.Count ?? 0,
            PacketsFiltered = _packetsFiltered,
            PacketsAllowed = _packetsAllowed,
            FilteringRate = _packetsAllowed + _packetsFiltered > 0
                ? (double)_packetsFiltered / (_packetsAllowed + _packetsFiltered) * 100.0
                : 0.0
        };
    }
    
    /// <summary>
    /// Gets pan value for a pilot (for spatial audio)
    /// </summary>
    public double GetPanForPilot(string pilotId)
    {
        if (_currentSelection == null)
            return 0.0; // Center pan (default)
        
        var pilot = _currentSelection.SelectedPilots
            .FirstOrDefault(p => p.PilotId == pilotId);
        
        return pilot?.Pan ?? 0.0;
    }
    
    /// <summary>
    /// Checks if a pilot is selected
    /// </summary>
    public bool IsPilotSelected(string pilotId)
    {
        if (_currentSelection == null)
            return false;
        
        return _currentSelection.SelectedPilots.Any(p => p.PilotId == pilotId);
    }
    
    /// <summary>
    /// Gets all selected pilot IDs
    /// </summary>
    public IReadOnlyList<string> GetSelectedPilotIds()
    {
        if (_currentSelection == null)
            return Array.Empty<string>();
        
        return _currentSelection.SelectedPilots
            .Select(p => p.PilotId)
            .ToList()
            .AsReadOnly();
    }
}

/// <summary>
/// Statistics about frequency filtering
/// </summary>
public class FilterStatistics
{
    public bool IsActive { get; set; }
    public int SelectedPilotCount { get; set; }
    public int PacketsFiltered { get; set; }
    public int PacketsAllowed { get; set; }
    public double FilteringRate { get; set; }
    
    public override string ToString()
    {
        return $"Filter: {(IsActive ? "Active" : "Inactive")}, Pilots: {SelectedPilotCount}, " +
               $"Allowed: {PacketsAllowed}, Filtered: {PacketsFiltered}, Rate: {FilteringRate:F1}%";
    }
}
