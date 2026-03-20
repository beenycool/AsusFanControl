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
        private readonly object _lock = new object();
        private readonly List<FanCurvePoint> _points = new List<FanCurvePoint>();

        public IReadOnlyList<FanCurvePoint> Points
        {
            get
            {
                lock (_lock)
                {
                    return _points.ToList();
                }
            }
        }

        public int GetTargetSpeed(int currentTemp)
        {
            var points = Points;
            if (points.Count == 0)
                return 0;

            var sortedPoints = points.ToList();
            bool isSorted = true;
            for (int i = 0; i < sortedPoints.Count - 1; i++)
            {
                if (sortedPoints[i].Temperature > sortedPoints[i + 1].Temperature)
                {
                    isSorted = false;
                    break;
                }
            }

            if (!isSorted)
            {
                sortedPoints.Sort((a, b) => a.Temperature.CompareTo(b.Temperature));
            }

            int count = sortedPoints.Count;
            if (currentTemp <= sortedPoints[0].Temperature)
                return sortedPoints[0].Speed;

            if (currentTemp >= sortedPoints[count - 1].Temperature)
                return sortedPoints[count - 1].Speed;

            for (int i = 0; i < count - 1; i++)
            {
                var p1 = sortedPoints[i];
                var p2 = sortedPoints[i + 1];

                if (currentTemp >= p1.Temperature && currentTemp <= p2.Temperature)
                {
                    if (p1.Temperature == p2.Temperature)
                        return p2.Speed;

                    double tRatio = (double)(currentTemp - p1.Temperature) / (p2.Temperature - p1.Temperature);
                    return (int)(p1.Speed + (p2.Speed - p1.Speed) * tRatio);
                }
            }

            return sortedPoints[count - 1].Speed;
        }

        public override string ToString()
        {
            return string.Join(",", Points.Select(p => $"{p.Temperature}:{p.Speed}"));
        }

        public void SetPoints(IEnumerable<FanCurvePoint> newPoints)
        {
            lock (_lock)
            {
                _points.Clear();
                if (newPoints == null)
                {
                    return;
                }

                foreach (var point in newPoints)
                {
                    _points.Add(new FanCurvePoint(point.Temperature, point.Speed));
                }
            }
        }

        public void AddPoint(FanCurvePoint point)
        {
            lock (_lock)
            {
                _points.Add(new FanCurvePoint(point.Temperature, point.Speed));
            }
        }

        public void RemovePointAt(int index)
        {
            lock (_lock)
            {
                if (index >= 0 && index < _points.Count)
                {
                    _points.RemoveAt(index);
                }
            }
        }

        public void UpdatePointAt(int index, FanCurvePoint point)
        {
            lock (_lock)
            {
                if (index >= 0 && index < _points.Count)
                {
                    _points[index] = new FanCurvePoint(point.Temperature, point.Speed);
                }
            }
        }

        public int PointCount
        {
            get
            {
                lock (_lock)
                {
                    return _points.Count;
                }
            }
        }

        public void ClearPoints()
        {
            lock (_lock)
            {
                _points.Clear();
            }
        }

        public static FanCurve FromString(string data)
        {
            var curve = new FanCurve();
            if (string.IsNullOrWhiteSpace(data))
                return curve;

            var points = new List<FanCurvePoint>();
            var parts = data.Split(',');
            foreach (var part in parts)
            {
                var kv = part.Split(':');
                if (kv.Length == 2 && int.TryParse(kv[0], out int temperature) && int.TryParse(kv[1], out int speed))
                {
                    points.Add(new FanCurvePoint(temperature, speed));
                }
            }

            curve.SetPoints(points);
            return curve;
        }
    }
}
