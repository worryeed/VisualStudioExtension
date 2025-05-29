using System.ComponentModel.Design;
using System.Threading.Tasks;

namespace CodeAIExtension.Commands;

internal sealed class AuthToolWindowCommand
{
    public const int CommandId = PackageIds.ShowToolWindowCmd;
    public static readonly Guid CommandSet = PackageGuids.CodeAIExtension;
    private readonly AsyncPackage _package;

    private AuthToolWindowCommand(AsyncPackage package, OleMenuCommandService mcs)
    {
        _package = package;
        var cmdID = new CommandID(CommandSet, CommandId);
        var cmd = new MenuCommand(ShowWindow, cmdID);
        mcs.AddCommand(cmd);
    }

    public static async Task InitializeAsync(AsyncPackage package)
    {
        var mcs = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
        new AuthToolWindowCommand(package, mcs);
    }

    private void ShowWindow(object sender, EventArgs e)
    {
        _ = _package.JoinableTaskFactory.RunAsync(async () =>
        {
            await _package.ShowToolWindowAsync(typeof(AuthToolWindow), 0, true, _package.DisposalToken);
        });
    }
}
