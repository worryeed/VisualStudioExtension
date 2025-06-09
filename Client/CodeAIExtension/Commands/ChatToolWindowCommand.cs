using CodeAIExtension.ViewModels;
using System.ComponentModel.Design;
using System.Threading.Tasks;

namespace CodeAIExtension.Commands;

internal sealed class ChatToolWindowCommand
{
    public const int CommandId = PackageIds.ShowChatWindowCmd;
    public static readonly Guid CommandSet = PackageGuids.CodeAIExtension;
    private readonly AsyncPackage _package;

    private ChatToolWindowCommand(AsyncPackage package, OleMenuCommandService mcs)
    {
        _package = package;
        var cmdID = new CommandID(CommandSet, CommandId);
        var cmd = new MenuCommand(ShowWindow, cmdID);
        mcs.AddCommand(cmd);
    }

    public static async Task InitializeAsync(AsyncPackage package)
    {
        var mcs = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
        new ChatToolWindowCommand(package, mcs);
    }

    private void ShowWindow(object sender, EventArgs e)
    {
        _ = _package.JoinableTaskFactory.RunAsync(async () =>
        {
            await _package.ShowToolWindowAsync(typeof(ChatToolWindow), 0, true, _package.DisposalToken);
        });
    }
}
