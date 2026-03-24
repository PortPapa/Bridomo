// Converters/InverseBoolConverter.cs
// bool 값을 반전시키는 컨버터.
// 프록시가 실행 중(IsRunning=true)일 때 설정 입력 필드를 비활성화하는 데 사용합니다.

using System.Globalization;
using System.Windows.Data;

namespace LocalTrafficInspector.Converters;

/// <summary>
/// true → false, false → true로 변환하는 IValueConverter
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return value;
    }
}
