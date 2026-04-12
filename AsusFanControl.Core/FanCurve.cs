using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace AsusFanControl.Core
{
    public class FanCurvePoint
    {
        [JsonPropertyName("t")]
        public int Temperature { get; set; }
        [JsonPropertyName("s")]
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
    private List<FanCurvePoint> _points = new List<FanCurvePoint>();

    [JsonPropertyName("p")]
    public List<FanCurvePoint> Points
    {
        get
        {
            lock (_lock)
            {
                return _points.Select(ClonePoint).ToList();
            }
        }
        set
        {
            SetPoints(value);
        }
    }

        public int GetTargetSpeed(int currentTemp)
        {
            List<FanCurvePoint> sortedPoints;
            lock (_lock)
            {
                if (_points.Count == 0)
                    return 0;

                sortedPoints = _points.Select(ClonePoint).ToList();
            }

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

        public string ToJson() => JsonSerializer.Serialize(this);

        public static FanCurve FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new FanCurve();
            try
            {
                return JsonSerializer.Deserialize<FanCurve>(json) ?? new FanCurve();
            }
            catch
            {
                return new FanCurve();
            }
        }

        public void SetPoints(IEnumerable<FanCurvePoint> newPoints)
        {
            var clonedPoints = newPoints?
                .Select(point => ClonePoint(ValidatePoint(point, nameof(newPoints))))
                .ToList() ?? new List<FanCurvePoint>();

            lock (_lock)
            {
                _points = clonedPoints;
            }
        }

        public void AddPoint(FanCurvePoint point)
        {
            var clonedPoint = ClonePoint(ValidatePoint(point, nameof(point)));

            lock (_lock)
            {
                _points.Add(clonedPoint);
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
            var clonedPoint = ClonePoint(ValidatePoint(point, nameof(point)));

            lock (_lock)
            {
                if (index >= 0 && index < _points.Count)
                {
                    _points[index] = clonedPoint;
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

public static FanCurve FromString(string data) => FromJson(data);

        private static FanCurvePoint ValidatePoint(FanCurvePoint point, string paramName)
        {
            if (point == null)
            {
                throw new ArgumentNullException(paramName, "Points cannot contain null entries.");
            }

            if (point.Temperature < 0 || point.Temperature > 100)
            {
                throw new ArgumentOutOfRangeException(paramName, "Point temperature must be between 0 and 100.");
            }

            if (point.Speed < 0 || point.Speed > 100)
            {
                throw new ArgumentOutOfRangeException(paramName, "Point speed must be between 0 and 100.");
            }

            return point;
        }

        private static FanCurvePoint ClonePoint(FanCurvePoint point)
        {
            return new FanCurvePoint(point.Temperature, point.Speed);
        }
    }
}
