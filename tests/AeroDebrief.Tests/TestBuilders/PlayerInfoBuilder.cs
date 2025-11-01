using AeroDebrief.Core;

namespace AeroDebrief.Tests.TestBuilders
{
    /// <summary>
    /// Builder for creating PlayerInfo test objects
    /// </summary>
    public class PlayerInfoBuilder
    {
        private string _name = "TestPilot";
        private string _transmitterGuid = Guid.NewGuid().ToString();
        private int _coalition = 2; // Blue by default
        private int _seat = 0;
        private bool _allowRecord = true;
        private Position _position = new Position();
        private AircraftInfo _aircraftInfo = new AircraftInfo();

        public PlayerInfoBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public PlayerInfoBuilder WithTransmitterGuid(string guid)
        {
            _transmitterGuid = guid;
            return this;
        }

        public PlayerInfoBuilder WithCoalition(int coalition)
        {
            _coalition = coalition;
            return this;
        }

        public PlayerInfoBuilder WithRedCoalition()
        {
            _coalition = 1;
            return this;
        }

        public PlayerInfoBuilder WithBlueCoalition()
        {
            _coalition = 2;
            return this;
        }

        public PlayerInfoBuilder WithSpectatorCoalition()
        {
            _coalition = 0;
            return this;
        }

        public PlayerInfoBuilder WithSeat(int seat)
        {
            _seat = seat;
            return this;
        }

        public PlayerInfoBuilder WithAllowRecord(bool allowRecord)
        {
            _allowRecord = allowRecord;
            return this;
        }

        public PlayerInfoBuilder WithPosition(Position position)
        {
            _position = position;
            return this;
        }

        public PlayerInfoBuilder WithPosition(Action<PositionBuilder> configure)
        {
            var builder = new PositionBuilder();
            configure(builder);
            _position = builder.Build();
            return this;
        }

        public PlayerInfoBuilder WithAircraftInfo(AircraftInfo aircraftInfo)
        {
            _aircraftInfo = aircraftInfo;
            return this;
        }

        public PlayerInfoBuilder WithAircraftInfo(Action<AircraftInfoBuilder> configure)
        {
            var builder = new AircraftInfoBuilder();
            configure(builder);
            _aircraftInfo = builder.Build();
            return this;
        }

        /// <summary>
        /// Creates a default F-16 pilot
        /// </summary>
        public PlayerInfoBuilder WithF16Pilot()
        {
            _name = "Viper1";
            _aircraftInfo = new AircraftInfoBuilder().WithF16().Build();
            return this;
        }

        /// <summary>
        /// Creates a default A-10 pilot
        /// </summary>
        public PlayerInfoBuilder WithA10Pilot()
        {
            _name = "Hawg1";
            _aircraftInfo = new AircraftInfoBuilder().WithA10().Build();
            return this;
        }

        public PlayerInfo Build()
        {
            return new PlayerInfo
            {
                Name = _name,
                TransmitterGuid = _transmitterGuid,
                Coalition = _coalition,
                Seat = _seat,
                AllowRecord = _allowRecord,
                Position = _position,
                AircraftInfo = _aircraftInfo
            };
        }
    }
}
