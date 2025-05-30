using CodeAIExtension.Views;
using System.Runtime.InteropServices;

namespace CodeAIExtension;

[Guid("38b634f4-035a-4259-9046-350f9674e7d0")]
public sealed class ChatToolWindow : ToolWindowPane
{
    public ChatToolWindow() : base(null)
    {
        this.Caption = "Chat AI";
        this.Content = new ChatToolWindowControl();
    }
}