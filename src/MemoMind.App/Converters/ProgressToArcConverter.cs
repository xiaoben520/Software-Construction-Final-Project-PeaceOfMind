using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MemoMind.App.Converters;

public class ProgressToArcConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double progress = value is double d ? d : 0.0;
        progress = Math.Max(0.0, Math.Min(1.0, progress));

        double cx = 45, cy = 45, r = 38;

        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(cx, cy - r), IsClosed = false };

        if (progress <= 0.0)
        {
            // No arc — return empty geometry
            return geometry;
        }

        if (progress >= 1.0)
        {
            // Full circle — draw a nearly full arc
            progress = 0.9999;
        }

        double angle = progress * 360.0;
        double radians = (angle - 90.0) * Math.PI / 180.0;
        double endX = cx + r * Math.Cos(radians);
        double endY = cy + r * Math.Sin(radians);
        bool isLargeArc = angle > 180.0;

        var arc = new ArcSegment(
            new Point(endX, endY),
            new Size(r, r),
            0,
            isLargeArc,
            SweepDirection.Clockwise,
            true);

        figure.Segments.Add(arc);
        geometry.Figures.Add(figure);

        return geometry;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
