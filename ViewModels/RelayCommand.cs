// ViewModels/RelayCommand.cs
// ICommand 구현체. XAML 버튼의 Command 바인딩에 사용됩니다.
// Execute(실행할 동작)와 CanExecute(실행 가능 여부)를 람다로 받습니다.

using System.Windows.Input;

namespace LocalTrafficInspector.ViewModels;

/// <summary>
/// 버튼 등 UI 요소에 바인딩할 수 있는 범용 커맨드
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>파라미터 없는 Action을 받는 편의 생성자</summary>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
    {
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>CanExecute 상태가 변경되었음을 UI에 알립니다.</summary>
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}

/// <summary>
/// 파라미터 타입이 지정된 제네릭 커맨드
/// </summary>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
        => _canExecute?.Invoke(parameter is T t ? t : default) ?? true;

    public void Execute(object? parameter)
        => _execute(parameter is T t ? t : default);
}
