using System;

namespace CallUp.Services
{
    public interface IGeoService
    {
        double CalculateDistance(double lat1, double lon1, double lat2, double lon2);
    }

    public class GeoService : IGeoService
    {
        /// <summary>
        /// Calculates the distance between two points in Kilometers using the Haversine formula.
        /// </summary>
        public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double EarthRadius = 6371; // In KM

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var distance = EarthRadius * c;

            return Math.Round(distance, 1);
        }

        private double ToRadians(double deg)
        {
            return deg * (Math.PI / 180);
        }
    }
}
