using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using ShalevOhad.DCS.SRS.Recorder.Core.Models;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.Core.Filtering
{
    /// <summary>Handles frequency filtering with modulation support</summary>
    public sealed class FrequencyFilter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private HashSet<(double Frequency, Modulation Modulation)> _selectedCombinations = new();
        private bool _enabled;

        public bool IsEnabled => _enabled;
        public IReadOnlySet<(double Frequency, Modulation Modulation)> SelectedCombinations => _selectedCombinations;

        public void SetFilter(IEnumerable<FrequencyModulationInfo> frequencyModulations)
        {
            _selectedCombinations = new HashSet<(double, Modulation)>(
                frequencyModulations.Select(fm => (fm.Frequency, fm.Modulation))
            );
            _enabled = _selectedCombinations.Count > 0;
            
            Logger.Info($"?? FREQUENCY FILTER SET: {_selectedCombinations.Count} combinations selected");
            foreach (var combo in _selectedCombinations.Take(10)) // Log first 10 for debugging
            {
                Logger.Info($"   - Selected: {combo.Frequency:F1} Hz, Modulation: {combo.Modulation}");
            }
            if (_selectedCombinations.Count > 10)
            {
                Logger.Info($"   ... and {_selectedCombinations.Count - 10} more combinations");
            }
        }

        public void ClearFilter()
        {
            _selectedCombinations.Clear();
            _enabled = false;
            
            Logger.Debug("Frequency filter cleared");
        }

        public bool ShouldIncludePacket(AudioPacketMetadata packet)
        {
            if (!_enabled)
            {
                Logger.Trace("Frequency filter disabled - including all packets");
                return true;
            }

            var modulation = Enum.IsDefined(typeof(Modulation), (int)packet.Modulation) 
                ? (Modulation)packet.Modulation 
                : Modulation.DISABLED;

            var shouldInclude = _selectedCombinations.Contains((packet.Frequency, modulation));
            
            if (!shouldInclude)
            {
                Logger.Trace($"?? Packet FILTERED OUT: Freq={packet.Frequency:F1} Hz, Modulation={modulation} (not in selected combinations)");
            }
            else
            {
                Logger.Trace($"?? Packet INCLUDED: Freq={packet.Frequency:F1} Hz, Modulation={modulation}");
            }
            
            return shouldInclude;
        }
    }
}