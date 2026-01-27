using AsusFanControl.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace AsusFanControlGUI
{
    public class FanCurveControl : UserControl
    {
        private List<FanCurvePoint> _points = new List<FanCurvePoint>();
        private int _draggingIndex = -1;
        private const int PointRadius = 6;
        private const int MarginLeft = 40;
        private const int MarginBottom = 30;
        private const int MarginRight = 20;
        private const int MarginTop = 20;

        public FanCurveControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(30, 30, 30); // Dark background
            this.Cursor = Cursors.Cross;
        }

        public void SetCurve(FanCurve curve)
        {
            _points.Clear();
            if (curve != null && curve.Points != null)
            {
                foreach (var p in curve.Points)
                {
                    _points.Add(new FanCurvePoint(p.Temperature, p.Speed));
                }
            }
            // Ensure sorted
            _points.Sort((a, b) => a.Temperature.CompareTo(b.Temperature));

            Invalidate();
        }

        public FanCurve GetCurve()
        {
            var curve = new FanCurve();
            // Deep copy
            foreach (var p in _points)
            {
                curve.Points.Add(new FanCurvePoint(p.Temperature, p.Speed));
            }
            // Sort
            curve.Points.Sort((a, b) => a.Temperature.CompareTo(b.Temperature));
            return curve;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = Width - MarginLeft - MarginRight;
            int h = Height - MarginTop - MarginBottom;

            // Draw Grid
            using (var gridPen = new Pen(Color.FromArgb(60, 60, 60), 1))
            {
                // Horizontal lines (Speed)
                for (int i = 0; i <= 10; i++)
                {
                    int y = Height - MarginBottom - (int)(i * 10 * h / 100.0);
                    g.DrawLine(gridPen, MarginLeft, y, Width - MarginRight, y);

                    // Labels
                    string label = (i * 10).ToString();
                    var size = g.MeasureString(label, Font);
                    g.DrawString(label, Font, Brushes.Gray, MarginLeft - size.Width - 2, y - size.Height / 2);
                }

                // Vertical lines (Temp)
                for (int i = 0; i <= 10; i++)
                {
                    int x = MarginLeft + (int)(i * 10 * w / 100.0);
                    g.DrawLine(gridPen, x, MarginTop, x, Height - MarginBottom);

                    // Labels
                    string label = (i * 10).ToString();
                    var size = g.MeasureString(label, Font);
                    g.DrawString(label, Font, Brushes.Gray, x - size.Width / 2, Height - MarginBottom + 2);
                }
            }

            // Draw Axis Labels
            g.DrawString("Temp (°C)", Font, Brushes.White, Width / 2 - 20, Height - 15);

            using (var sf = new StringFormat())
            {
                sf.FormatFlags = StringFormatFlags.DirectionVertical;
                g.DrawString("Speed (%)", Font, Brushes.White, 0, Height / 2 + 20, sf);
            }

            // Draw Curve
            if (_points.Count > 0)
            {
                var points = _points.Select(p => PointToClient(p)).ToArray();

                using (var linePen = new Pen(Color.Chartreuse, 2))
                {
                    g.DrawLines(linePen, points);
                }

                // Draw Area under curve (optional, like MSI)
                using (var brush = new LinearGradientBrush(new Rectangle(MarginLeft, MarginTop, w, h), Color.FromArgb(100, Color.Chartreuse), Color.Transparent, LinearGradientMode.Vertical))
                {
                    var path = new GraphicsPath();
                    path.AddLines(points);
                    path.AddLine(points.Last().X, Height - MarginBottom, points.First().X, Height - MarginBottom);
                    path.CloseFigure();
                    g.FillPath(brush, path);
                }

                // Draw Points
                foreach (var pt in points)
                {
                    g.FillEllipse(Brushes.White, pt.X - PointRadius, pt.Y - PointRadius, PointRadius * 2, PointRadius * 2);
                    g.DrawEllipse(Pens.Black, pt.X - PointRadius, pt.Y - PointRadius, PointRadius * 2, PointRadius * 2);
                }

                // Highlight dragging point
                if (_draggingIndex >= 0 && _draggingIndex < points.Length)
                {
                    var pt = points[_draggingIndex];
                    g.DrawEllipse(Pens.Red, pt.X - PointRadius - 2, pt.Y - PointRadius - 2, (PointRadius + 2) * 2, (PointRadius + 2) * 2);

                    // Show coordinates
                    string coord = $"({_points[_draggingIndex].Temperature}°C, {_points[_draggingIndex].Speed}%)";
                    g.DrawString(coord, Font, Brushes.Chartreuse, pt.X + 10, pt.Y - 20);
                }
            }
        }

        private Point PointToClient(FanCurvePoint p)
        {
            int w = Width - MarginLeft - MarginRight;
            int h = Height - MarginTop - MarginBottom;

            int x = MarginLeft + (int)(p.Temperature * w / 100.0);
            int y = Height - MarginBottom - (int)(p.Speed * h / 100.0);
            return new Point(x, y);
        }

        private FanCurvePoint ClientToPoint(Point p)
        {
            int w = Width - MarginLeft - MarginRight;
            int h = Height - MarginTop - MarginBottom;

            int temp = (int)((p.X - MarginLeft) * 100.0 / w);
            int speed = (int)((Height - MarginBottom - p.Y) * 100.0 / h);

            return new FanCurvePoint(
                Math.Max(0, Math.Min(100, temp)),
                Math.Max(0, Math.Min(100, speed))
            );
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            // Check for hit
            for (int i = 0; i < _points.Count; i++)
            {
                var pt = PointToClient(_points[i]);
                if (Math.Pow(e.X - pt.X, 2) + Math.Pow(e.Y - pt.Y, 2) <= Math.Pow(PointRadius + 2, 2))
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        _draggingIndex = i;
                        return;
                    }
                    else if (e.Button == MouseButtons.Right)
                    {
                        // Remove point
                        _points.RemoveAt(i);
                        Invalidate();
                        return;
                    }
                }
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left)
            {
                // Add point
                var newPoint = ClientToPoint(e.Location);

                // Check if we are too close to existing point
                if (_points.Any(p => Math.Abs(p.Temperature - newPoint.Temperature) < 2))
                    return;

                _points.Add(newPoint);
                _points.Sort((a, b) => a.Temperature.CompareTo(b.Temperature));
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_draggingIndex >= 0 && e.Button == MouseButtons.Left)
            {
                var newPos = ClientToPoint(e.Location);

                var currentPoint = _points[_draggingIndex];
                currentPoint.Temperature = newPos.Temperature;
                currentPoint.Speed = newPos.Speed;

                _points.Sort((a, b) => a.Temperature.CompareTo(b.Temperature));

                // Update dragging index to track the sorted point
                _draggingIndex = _points.IndexOf(currentPoint);

                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _draggingIndex = -1;
            Invalidate();
        }
    }
}
