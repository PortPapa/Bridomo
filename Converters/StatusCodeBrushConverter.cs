using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LocalTrafficInspector.Converters;

/// <summary>
/// HTTP 상태코드에 따라 색상을 반환합니다.
/// 2xx=초록, 3xx=파랑, 4xx=주황, 5xx=빨강
/// </summary>
public class StatusCodeBrushConverter : IValueConverter
{
    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(63, 185, 80));
    private static readonly Brush Blue = new SolidColorBrush(Color.FromRgb(88, 166, 255));
    private static readonly Brush Orange = new SolidColorBrush(Color.FromRgb(240, 136, 62));
    private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(248, 81, 73));
    private static readonly Brush Gray = new SolidColorBrush(Color.FromRgb(125, 133, 144));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int code)
        {
            return code switch
            {
                >= 200 and < 300 => Green,
                >= 300 and < 400 => Blue,
                >= 400 and < 500 => Orange,
                >= 500 => Red,
                _ => Gray
            };
        }
        return Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
