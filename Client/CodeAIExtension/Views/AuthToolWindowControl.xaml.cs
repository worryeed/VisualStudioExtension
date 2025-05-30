using CodeAIExtension.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace CodeAIExtension.Views;

public partial class AuthToolWindowControl : UserControl
{
    public AuthToolWindowControl()
    {
        InitializeComponent();
        DataContext = CodeAIExtensionPackage.Instance.Services.GetRequiredService<AuthToolWindowViewModel>();
    }
}
