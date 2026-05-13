using System.Windows;
using System.Windows.Media;

namespace Dock.App.Services;

public static class SpecialIconService
{
    public static ImageSource GetWindowsLogo()
    {
        var group = new DrawingGroup();
        var brush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
        var gap = 2.0;
        var tile = 18.0;

        group.Children.Add(CreatePane(8, 8, tile, brush));
        group.Children.Add(CreatePane(8 + tile + gap, 8, tile, brush));
        group.Children.Add(CreatePane(8, 8 + tile + gap, tile, brush));
        group.Children.Add(CreatePane(8 + tile + gap, 8 + tile + gap, tile, brush));

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    public static ImageSource GetWindowIcon()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromRgb(58, 95, 150)),
            new Pen(new SolidColorBrush(Color.FromRgb(16, 28, 44)), 1.5),
            new RectangleGeometry(new Rect(8, 11, 40, 31), 4, 4)));
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromRgb(124, 184, 255)),
            null,
            new RectangleGeometry(new Rect(11, 15, 34, 7), 2, 2)));
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
            null,
            new RectangleGeometry(new Rect(13, 26, 30, 11), 2, 2)));

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    public static ImageSource GetDropPlaceholderIcon()
    {
        var group = new DrawingGroup();
        var brush = new SolidColorBrush(Color.FromArgb(180, 96, 255, 160));
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(220, 210, 255, 230)), 3)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromArgb(48, 96, 255, 160)),
            new Pen(brush, 1.5),
            new RectangleGeometry(new Rect(9, 9, 38, 38), 8, 8)));
        group.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(28, 17), new Point(28, 39))));
        group.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(17, 28), new Point(39, 28))));

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    private static GeometryDrawing CreatePane(double x, double y, double size, Brush brush)
    {
        return new GeometryDrawing(
            brush,
            null,
            new RectangleGeometry(new Rect(x, y, size, size), 1.5, 1.5));
    }
}
