using CodeAIExtension.Views;
using System.Runtime.InteropServices;

namespace CodeAIExtension;

[Guid("9C0A5EDE-7A0F-4F63-8C55-851C7F1B2AAB")]
public sealed class AuthToolWindow : ToolWindowPane
{
    public AuthToolWindow() : base(null)
    {
        this.Caption = "Code AI";
        this.Content = new AuthToolWindowControl();
    }
}