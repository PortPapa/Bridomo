// ViewModels/BaseViewModel.cs
// 모든 ViewModel의 부모 클래스. INotifyPropertyChanged를 구현하여
// UI 바인딩이 속성 변경을 감지할 수 있게 합니다.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LocalTrafficInspector.ViewModels;

/// <summary>
/// MVVM의 핵심: 속성이 바뀌면 UI에 자동 통보하는 기반 클래스
/// </summary>
public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 속성 값을 변경하고, 값이 실제로 바뀌었을 때만 UI에 통보합니다.
    /// 사용 예: SetProperty(ref _name, value);
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// PropertyChanged 이벤트를 발생시킵니다.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
