using CodeAIExtension.Services;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CodeAIExtension.ViewModels;

public class AuthToolWindowViewModel : INotifyPropertyChanged
{
    private readonly IAuthService _authService;
    private string _statusText;
    private string _actionText;
    public event PropertyChangedEventHandler PropertyChanged;

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
    }

    public string ActionText
    {
        get => _actionText;
        private set { _actionText = value; OnPropertyChanged(nameof(ActionText)); }
    }

    public ICommand ActionCommand { get; }

    public AuthToolWindowViewModel(IAuthService authService)
    {
        _authService = authService;
        _authService.StateChanged += (s, e) => UpdateProperties();
        ActionCommand = new AsyncRelayCommand(OnActionAsync);
        UpdateProperties();
    }

    private void UpdateProperties()
    {
        if (_authService.IsAuthenticated)
        {
            StatusText = "Вы вошли";
            ActionText = "Выйти";
        }
        else
        {
            StatusText = "Требуется вход";
            ActionText = "Войти";
        }
    }

    private async Task OnActionAsync()
    {
        if (_authService.IsAuthenticated)
        {
            await _authService.LogoutAsync();
        }
        else
        {
            _authService.SignIn();
        }

        UpdateProperties();
    }

    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
