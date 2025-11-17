using System;
using System.Collections.Generic;
using SkiaSharp;

namespace AeroDebrief.UI.Charts
{
    /// <summary>
    /// Provides unique marker geometries for pilot visualization.
    /// Phase 4: Generates 32+ distinct shapes to support high-pilot-count frequencies.
    /// </summary>
    public static class PilotMarkers
    {
        /// <summary>
        /// Cache of pilot-to-geometry assignments for consistency and performance.
        /// </summary>
        private static readonly Dictionary<string, SKPath> _markerCache = new();

        /// <summary>
        /// Available marker types with multiple variants.
        /// </summary>
        private enum MarkerType
        {
            Circle,
            Square,
            Triangle,
            Diamond,
            Pentagon,
            Hexagon,
            Star4,
            Star5,
            Star6,
            Cross,
            Plus,
            XShape,
            Chevron,
            InvertedTriangle,
            TriangleRight,
            TriangleLeft,
            Octagon,
            Heptagon,
            Star8,
            RoundedSquare,
            Teardrop,
            Heart,
            Kite,
            Arrow,
            House,
            Bow,
            Crescent,
            Ring,
            DoubleDiamond,
            TripleCircle,
            SquareCross,
            DiamondCross
        }

        /// <summary>
        /// Gets a unique marker geometry for the specified pilot ID.
        /// Same pilot ID always returns the same marker shape.
        /// </summary>
        /// <param name="pilotId">Unique pilot identifier</param>
        /// <param name="size">Marker size in pixels (default 8)</param>
        /// <returns>SKPath representing the marker geometry</returns>
        public static SKPath GetMarkerForPilot(string pilotId, float size = 8f)
        {
            if (string.IsNullOrEmpty(pilotId))
            {
                return CreateCircle(size);
            }

            var cacheKey = $"{pilotId}:{size}";
            
            // Check cache first
            if (_markerCache.TryGetValue(cacheKey, out var cachedMarker))
            {
                return cachedMarker;
            }

            // Generate deterministic marker type from pilot ID
            var hash = GetStableHashCode(pilotId);
            var markerTypeCount = Enum.GetValues(typeof(MarkerType)).Length;
            var markerType = (MarkerType)(Math.Abs(hash) % markerTypeCount);

            // Create the marker geometry
            var marker = CreateMarker(markerType, size);

            // Cache for future lookups
            _markerCache[cacheKey] = marker;

            return marker;
        }

        /// <summary>
        /// Clears the marker cache. Used when resetting visualization or for testing.
        /// </summary>
        public static void ClearCache()
        {
            foreach (var path in _markerCache.Values)
            {
                path?.Dispose();
            }
            _markerCache.Clear();
        }

        /// <summary>
        /// Gets the total number of unique marker types available.
        /// </summary>
        public static int MarkerTypeCount => Enum.GetValues(typeof(MarkerType)).Length;

        private static SKPath CreateMarker(MarkerType type, float size)
        {
            return type switch
            {
                MarkerType.Circle => CreateCircle(size),
                MarkerType.Square => CreateSquare(size),
                MarkerType.Triangle => CreateTriangle(size),
                MarkerType.Diamond => CreateDiamond(size),
                MarkerType.Pentagon => CreateRegularPolygon(5, size),
                MarkerType.Hexagon => CreateRegularPolygon(6, size),
                MarkerType.Star4 => CreateStar(4, size),
                MarkerType.Star5 => CreateStar(5, size),
                MarkerType.Star6 => CreateStar(6, size),
                MarkerType.Cross => CreateCross(size),
                MarkerType.Plus => CreatePlus(size),
                MarkerType.XShape => CreateX(size),
                MarkerType.Chevron => CreateChevron(size),
                MarkerType.InvertedTriangle => CreateInvertedTriangle(size),
                MarkerType.TriangleRight => CreateTriangleRight(size),
                MarkerType.TriangleLeft => CreateTriangleLeft(size),
                MarkerType.Octagon => CreateRegularPolygon(8, size),
                MarkerType.Heptagon => CreateRegularPolygon(7, size),
                MarkerType.Star8 => CreateStar(8, size),
                MarkerType.RoundedSquare => CreateRoundedSquare(size),
                MarkerType.Teardrop => CreateTeardrop(size),
                MarkerType.Heart => CreateHeart(size),
                MarkerType.Kite => CreateKite(size),
                MarkerType.Arrow => CreateArrow(size),
                MarkerType.House => CreateHouse(size),
                MarkerType.Bow => CreateBow(size),
                MarkerType.Crescent => CreateCrescent(size),
                MarkerType.Ring => CreateRing(size),
                MarkerType.DoubleDiamond => CreateDoubleDiamond(size),
                MarkerType.TripleCircle => CreateTripleCircle(size),
                MarkerType.SquareCross => CreateSquareCross(size),
                MarkerType.DiamondCross => CreateDiamondCross(size),
                _ => CreateCircle(size)
            };
        }

        // Basic shapes
        private static SKPath CreateCircle(float size)
        {
            var path = new SKPath();
            path.AddCircle(0, 0, size / 2);
            return path;
        }

        private static SKPath CreateSquare(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            path.AddRect(new SKRect(-half, -half, half, half));
            return path;
        }

        private static SKPath CreateTriangle(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            path.MoveTo(0, -half);
            path.LineTo(half, half);
            path.LineTo(-half, half);
            path.Close();
            return path;
        }

        private static SKPath CreateDiamond(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            path.MoveTo(0, -half);
            path.LineTo(half, 0);
            path.LineTo(0, half);
            path.LineTo(-half, 0);
            path.Close();
            return path;
        }

        // Polygons
        private static SKPath CreateRegularPolygon(int sides, float size)
        {
            var path = new SKPath();
            var radius = size / 2;
            var angleStep = 2 * Math.PI / sides;

            for (int i = 0; i < sides; i++)
            {
                var angle = i * angleStep - Math.PI / 2;
                var x = (float)(radius * Math.Cos(angle));
                var y = (float)(radius * Math.Sin(angle));

                if (i == 0)
                    path.MoveTo(x, y);
                else
                    path.LineTo(x, y);
            }
            path.Close();
            return path;
        }

        // Stars
        private static SKPath CreateStar(int points, float size)
        {
            var path = new SKPath();
            var outerRadius = size / 2;
            var innerRadius = outerRadius * 0.4f;
            var angleStep = Math.PI / points;

            for (int i = 0; i < points * 2; i++)
            {
                var angle = i * angleStep - Math.PI / 2;
                var radius = (i % 2 == 0) ? outerRadius : innerRadius;
                var x = (float)(radius * Math.Cos(angle));
                var y = (float)(radius * Math.Sin(angle));

                if (i == 0)
                    path.MoveTo(x, y);
                else
                    path.LineTo(x, y);
            }
            path.Close();
            return path;
        }

        // Cross and Plus
        private static SKPath CreateCross(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            var width = size * 0.25f;
            
            // Diagonal cross
            path.MoveTo(-half + width, -half);
            path.LineTo(0, -width);
            path.LineTo(half - width, -half);
            path.LineTo(half, -half + width);
            path.LineTo(width, 0);
            path.LineTo(half, half - width);
            path.LineTo(half - width, half);
            path.LineTo(0, width);
            path.LineTo(-half + width, half);
            path.LineTo(-half, half - width);
            path.LineTo(-width, 0);
            path.LineTo(-half, -half + width);
            path.Close();
            return path;
        }

        private static SKPath CreatePlus(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            var width = size * 0.2f;
            
            // Vertical bar
            path.AddRect(new SKRect(-width, -half, width, half));
            // Horizontal bar
            path.AddRect(new SKRect(-half, -width, half, width));
            return path;
        }

        private static SKPath CreateX(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            var offset = size * 0.15f;
            
            path.MoveTo(-half, -half + offset);
            path.LineTo(-half + offset, -half);
            path.LineTo(0, -offset);
            path.LineTo(half - offset, -half);
            path.LineTo(half, -half + offset);
            path.LineTo(offset, 0);
            path.LineTo(half, half - offset);
            path.LineTo(half - offset, half);
            path.LineTo(0, offset);
            path.LineTo(-half + offset, half);
            path.LineTo(-half, half - offset);
            path.LineTo(-offset, 0);
            path.Close();
            return path;
        }

        // Special shapes
        private static SKPath CreateChevron(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            path.MoveTo(0, -half);
            path.LineTo(half, 0);
            path.LineTo(0, half * 0.5f);
            path.LineTo(-half, 0);
            path.Close();
            return path;
        }

        private static SKPath CreateInvertedTriangle(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            path.MoveTo(0, half);
            path.LineTo(half, -half);
            path.LineTo(-half, -half);
            path.Close();
            return path;
        }

        private static SKPath CreateTriangleRight(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            path.MoveTo(half, 0);
            path.LineTo(-half, half);
            path.LineTo(-half, -half);
            path.Close();
            return path;
        }

        private static SKPath CreateTriangleLeft(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            path.MoveTo(-half, 0);
            path.LineTo(half, half);
            path.LineTo(half, -half);
            path.Close();
            return path;
        }

        private static SKPath CreateRoundedSquare(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            var radius = size * 0.2f;
            path.AddRoundRect(new SKRect(-half, -half, half, half), radius, radius);
            return path;
        }

        private static SKPath CreateTeardrop(float size)
        {
            var path = new SKPath();
            var radius = size / 2;
            path.AddCircle(0, radius * 0.3f, radius * 0.7f);
            path.MoveTo(0, -radius);
            path.LineTo(radius * 0.3f, radius * 0.3f);
            path.LineTo(-radius * 0.3f, radius * 0.3f);
            path.Close();
            return path;
        }

        private static SKPath CreateHeart(float size)
        {
            var path = new SKPath();
            var scale = size / 20f;
            
            path.MoveTo(0, 3 * scale);
            path.CubicTo(-5 * scale, -3 * scale, -10 * scale, 0, -5 * scale, 7 * scale);
            path.LineTo(0, 10 * scale);
            path.LineTo(5 * scale, 7 * scale);
            path.CubicTo(10 * scale, 0, 5 * scale, -3 * scale, 0, 3 * scale);
            path.Close();
            return path;
        }

        private static SKPath CreateKite(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            path.MoveTo(0, -half);
            path.LineTo(half * 0.5f, 0);
            path.LineTo(0, half);
            path.LineTo(-half * 0.5f, 0);
            path.Close();
            return path;
        }

        private static SKPath CreateArrow(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            path.MoveTo(0, -half);
            path.LineTo(half, 0);
            path.LineTo(half * 0.3f, 0);
            path.LineTo(half * 0.3f, half);
            path.LineTo(-half * 0.3f, half);
            path.LineTo(-half * 0.3f, 0);
            path.LineTo(-half, 0);
            path.Close();
            return path;
        }

        private static SKPath CreateHouse(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            // Roof
            path.MoveTo(0, -half);
            path.LineTo(half, 0);
            path.LineTo(half, half);
            path.LineTo(-half, half);
            path.LineTo(-half, 0);
            path.Close();
            return path;
        }

        private static SKPath CreateBow(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            path.MoveTo(-half, -half);
            path.QuadTo(0, 0, -half, half);
            path.LineTo(half, half * 0.7f);
            path.QuadTo(0, 0, half, -half * 0.7f);
            path.Close();
            return path;
        }

        private static SKPath CreateCrescent(float size)
        {
            var path = new SKPath();
            var radius = size / 2;
            path.AddCircle(0, 0, radius);
            path.AddCircle(radius * 0.3f, 0, radius * 0.7f, SKPathDirection.CounterClockwise);
            return path;
        }

        private static SKPath CreateRing(float size)
        {
            var path = new SKPath();
            var outerRadius = size / 2;
            var innerRadius = outerRadius * 0.5f;
            path.AddCircle(0, 0, outerRadius);
            path.AddCircle(0, 0, innerRadius, SKPathDirection.CounterClockwise);
            return path;
        }

        private static SKPath CreateDoubleDiamond(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            var quarter = size / 4;
            
            // First diamond
            path.MoveTo(0, -half);
            path.LineTo(quarter, -quarter);
            path.LineTo(0, 0);
            path.LineTo(-quarter, -quarter);
            path.Close();
            
            // Second diamond
            path.MoveTo(0, 0);
            path.LineTo(quarter, quarter);
            path.LineTo(0, half);
            path.LineTo(-quarter, quarter);
            path.Close();
            
            return path;
        }

        private static SKPath CreateTripleCircle(float size)
        {
            var path = new SKPath();
            var radius = size / 5;
            path.AddCircle(0, -radius * 1.5f, radius);
            path.AddCircle(-radius * 1.3f, radius * 0.75f, radius);
            path.AddCircle(radius * 1.3f, radius * 0.75f, radius);
            return path;
        }

        private static SKPath CreateSquareCross(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            var third = size / 3;
            
            // Square outline
            path.AddRect(new SKRect(-half, -half, half, half));
            // Cross inside
            path.MoveTo(0, -half);
            path.LineTo(0, half);
            path.MoveTo(-half, 0);
            path.LineTo(half, 0);
            
            return path;
        }

        private static SKPath CreateDiamondCross(float size)
        {
            var path = new SKPath();
            var half = size / 2;
            
            // Diamond
            path.MoveTo(0, -half);
            path.LineTo(half, 0);
            path.LineTo(0, half);
            path.LineTo(-half, 0);
            path.Close();
            
            // Cross inside
            path.MoveTo(0, -half * 0.6f);
            path.LineTo(0, half * 0.6f);
            path.MoveTo(-half * 0.6f, 0);
            path.LineTo(half * 0.6f, 0);
            
            return path;
        }

        /// <summary>
        /// Generate a stable hash code that's consistent across runs.
        /// </summary>
        private static int GetStableHashCode(string str)
        {
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
    }
}
