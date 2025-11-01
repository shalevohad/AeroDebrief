using AeroDebrief.Core;

namespace AeroDebrief.Tests.TestBuilders
{
    /// <summary>
    /// Builder for creating Position test objects
    /// </summary>
    public class PositionBuilder
    {
        private double _latitude = 0;
        private double _longitude = 0;
        private double _altitude = 0;

        public PositionBuilder WithLatitude(double latitude)
        {
            _latitude = latitude;
            return this;
        }

        public PositionBuilder WithLongitude(double longitude)
        {
            _longitude = longitude;
            return this;
        }

        public PositionBuilder WithAltitude(double altitude)
        {
            _altitude = altitude;
            return this;
        }

        /// <summary>
        /// Sets a valid position with default coordinates
        /// </summary>
        public PositionBuilder WithValidPosition()
        {
            _latitude = 40.7128;  // New York City
            _longitude = -74.0060;
            _altitude = 100;
            return this;
        }

        /// <summary>
        /// Sets an invalid position (zeros)
        /// </summary>
        public PositionBuilder WithInvalidPosition()
        {
            _latitude = 0;
            _longitude = 0;
            _altitude = 0;
            return this;
        }

        public Position Build()
        {
            return new Position
            {
                Latitude = _latitude,
                Longitude = _longitude,
                Altitude = _altitude
            };
        }
    }
}
