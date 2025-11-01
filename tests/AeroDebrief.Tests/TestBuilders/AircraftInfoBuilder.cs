using AeroDebrief.Core;

namespace AeroDebrief.Tests.TestBuilders
{
    /// <summary>
    /// Builder for creating AircraftInfo test objects
    /// </summary>
    public class AircraftInfoBuilder
    {
        private string _unitType = string.Empty;
        private uint _unitId = 0;

        public AircraftInfoBuilder WithUnitType(string unitType)
        {
            _unitType = unitType;
            return this;
        }

        public AircraftInfoBuilder WithUnitId(uint unitId)
        {
            _unitId = unitId;
            return this;
        }

        /// <summary>
        /// Creates a default F-16 aircraft
        /// </summary>
        public AircraftInfoBuilder WithF16()
        {
            _unitType = "F-16C_50";
            _unitId = 1001;
            return this;
        }

        /// <summary>
        /// Creates a default A-10 aircraft
        /// </summary>
        public AircraftInfoBuilder WithA10()
        {
            _unitType = "A-10C";
            _unitId = 1002;
            return this;
        }

        /// <summary>
        /// Creates a default F/A-18 aircraft
        /// </summary>
        public AircraftInfoBuilder WithFA18()
        {
            _unitType = "FA-18C_hornet";
            _unitId = 1003;
            return this;
        }

        public AircraftInfo Build()
        {
            return new AircraftInfo
            {
                UnitType = _unitType,
                UnitId = _unitId
            };
        }
    }
}
