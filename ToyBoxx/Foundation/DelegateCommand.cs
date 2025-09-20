using System.Windows.Input;

namespace ToyBoxx.Foundation;

public class DelegateCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private bool _isExecuting;
    private readonly object _syncLock = new();

    public DelegateCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool IsExecuting
    {
        get { lock (_syncLock) return _isExecuting; }
        private set { lock (_syncLock) _isExecuting = value; }
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

    public void Execute(object? parameter)
    {
        _ = _execute(parameter);
    }

    public async Task ExecuteAsync(object? parameter)
    {
        if (_execute is not null)
        {
            await _execute.Invoke(parameter);
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}