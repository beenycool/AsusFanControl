using System;
using System.Collections.Generic;
using System.Linq;

namespace AsusFanControl.Core
{
    public class FanCurvePoint
    {
        public int Temperature { get; set; }
        public int Speed { get; set; }

        public FanCurvePoint(int temperature, int speed)
        {
            Temperature = temperature;
            Speed = speed;
        }

        public FanCurvePoint() { }
    }

    public class FanCurve
    {
        public List<FanCurvePoint> Points { get; set; } = new List<FanCurvePoint>();

        public int GetTargetSpeed(int currentTemp)
        {
            var points = Points;
            if (points == null || points.Count == 0)
                return 0;

            // Optimization: Avoid allocation if already sorted
            List<FanCurvePoint> sortedPoints = points;
            bool isSorted = true;
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (points[i].Temperature > points[i + 1].Temperature)
                {
                    isSorted = false;
                    break;
                }
            }

            if (!isSorted)
            {
                // Sort points by temperature
                sortedPoints = points.OrderBy(p => p.Temperature).ToList();
            }

            // If below first point
            if (currentTemp <= sortedPoints[0].Temperature)
                return sortedPoints[0].Speed;

            // If above last point
            if (currentTemp >= sortedPoints[sortedPoints.Count - 1].Temperature)
                return sortedPoints[sortedPoints.Count - 1].Speed;

            // Interpolate
            for (int i = 0; i < sortedPoints.Count - 1; i++)
            {
                var p1 = sortedPoints[i];
                var p2 = sortedPoints[i + 1];

                if (currentTemp >= p1.Temperature && currentTemp <= p2.Temperature)
                {
                    if (p1.Temperature == p2.Temperature)
                        return p2.Speed;

                    // Linear interpolation
                    // speed = s1 + (s2 - s1) * (temp - t1) / (t2 - t1)
                    double tRatio = (double)(currentTemp - p1.Temperature) / (p2.Temperature - p1.Temperature);
                    int speed = (int)(p1.Speed + (p2.Speed - p1.Speed) * tRatio);
                    return speed;
                }
            }

            return sortedPoints[sortedPoints.Count - 1].Speed;
        }

        public override string ToString()
        {
            // Simple serialization: "temp:speed,temp:speed"
            return string.Join(",", Points.Select(p => $"{p.Temperature}:{p.Speed}"));
        }

        public static FanCurve FromString(string data)
        {
            var curve = new FanCurve();
            if (string.IsNullOrWhiteSpace(data))
                return curve;

            var parts = data.Split(',');
            foreach (var part in parts)
            {
                var kv = part.Split(':');
                if (kv.Length == 2 && int.TryParse(kv[0], out int t) && int.TryParse(kv[1], out int s))
                {
                    curve.Points.Add(new FanCurvePoint(t, s));
                }
            }
            return curve;
        }
    }
}
