using CodeAIExtension.Services;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CodeAIExtension;

public partial class AuthToolWindowControl : UserControl
{
    private readonly Dispatcher _uiDispatcher;
    private readonly AuthService _auth = CodeAIExtensionPackage.Auth;
    public AuthToolWindowControl()
    {
        InitializeComponent();

        _uiDispatcher = this.Dispatcher;
        _auth.StateChanged += Auth_StateChanged;
        Action.Click += OnAction_Click;

        UpdateUI();
    }

    private void Auth_StateChanged(object sender, EventArgs e)
    {
        _uiDispatcher.BeginInvoke(new Action(UpdateUI));
    }

    private void UpdateUI()
    {
        if (_auth.IsAuthenticated)
        {
            Status.Text = "Вы вошли";
            Action.Content = "Выйти";
        }
        else
        {
            Status.Text = "Требуется вход";
            Action.Content = "Войти";
        }
    }
    private void OnAction_Click(object sender, RoutedEventArgs e)
    {
        _ = OnActionAsync();  
    }

    private async Task OnActionAsync()
    {
        if (_auth.IsAuthenticated)
            await _auth.LogoutAsync();
        else
            _auth.SignIn();
        UpdateUI();
    }
}
