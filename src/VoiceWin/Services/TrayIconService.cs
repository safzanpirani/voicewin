using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VoiceWin.Services;

public class TrayIconService
{
    private readonly BitmapImage _baseIcon;
    private readonly Dictionary<TrayStatus, ImageSource> _iconCache = new();

    public TrayIconService()
    {
        _baseIcon = new BitmapImage();
        _baseIcon.BeginInit();
        _baseIcon.UriSource = new Uri("pack://application:,,,/Assets/vwtrans-crop.ico");
        _baseIcon.DecodePixelWidth = 256;
        _baseIcon.DecodePixelHeight = 256;
        _baseIcon.EndInit();
        _baseIcon.Freeze();

        _iconCache[TrayStatus.Ready] = CreateIconWithDot(Colors.LimeGreen);
        _iconCache[TrayStatus.Recording] = CreateIconWithDot(Colors.Red);
        _iconCache[TrayStatus.Processing] = CreateIconWithDot(Colors.Orange);
    }

    public ImageSource CreateIconWithStatus(TrayStatus status)
    {
        return _iconCache[status];
    }

    private ImageSource CreateIconWithDot(Color dotColor)
    {
        var size = 256.0;
        var dotSize = 72.0;
        var margin = 8.0;

        var drawingVisual = new DrawingVisual();
        using (var dc = drawingVisual.RenderOpen())
        {
            dc.DrawImage(_baseIcon, new Rect(0, 0, size, size));

            var dotX = size - dotSize - margin;
            var dotY = size - dotSize - margin;
            var center = new Point(dotX + dotSize / 2, dotY + dotSize / 2);

            dc.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                null,
                center,
                dotSize / 2 + 6,
                dotSize / 2 + 6);

            dc.DrawEllipse(
                new SolidColorBrush(dotColor),
                null,
                center,
                dotSize / 2,
                dotSize / 2);
        }

        var renderTarget = new RenderTargetBitmap(
            (int)size, (int)size,
            96, 96,
            PixelFormats.Pbgra32);

        renderTarget.Render(drawingVisual);
        renderTarget.Freeze();

        return renderTarget;
    }
}

public enum TrayStatus
{
    Ready,
    Recording,
    Processing
}
