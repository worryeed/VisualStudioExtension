using System.Threading.Tasks;
using System.Windows.Input;

namespace CodeAIExtension.Services;

public class AsyncRelayCommand : ICommand
{
	private readonly Func<Task> _executeAsync;
	private readonly Func<bool> _canExecute;

	public AsyncRelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
	{
		_executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
		_canExecute = canExecute;
	}

	public bool CanExecute(object parameter)
	{
		return _canExecute?.Invoke() ?? true;
	}

	public void Execute(object parameter)
	{
		_ = ExecuteInternalAsync();
	}

	private async Task ExecuteInternalAsync()
	{
		try
		{
			await _executeAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"AsyncRelayCommand error: {ex}");
		}
	}

	public event EventHandler CanExecuteChanged;

	public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
