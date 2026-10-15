using System.Windows.Input;

namespace GameShift.App.ViewModels;

public sealed class RelayCommand : ICommand
{
    readonly Action<object?> _run;
    readonly Func<object?, bool>? _canRun;

    public RelayCommand(Action<object?> run, Func<object?, bool>? canRun = null)
    {
        _run = run;
        _canRun = canRun;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canRun?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _run(parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
