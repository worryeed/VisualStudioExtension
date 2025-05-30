using CodeAIExtension.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;
using System.Windows.Controls;

namespace CodeAIExtension.Views;
public partial class ChatToolWindowControl : UserControl
{
    public ChatToolWindowControl()
    {
        InitializeComponent();
        DataContext = CodeAIExtensionPackage.Instance.Services.GetRequiredService<ChatToolWindowViewModel>();
    }
}
