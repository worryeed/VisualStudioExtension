global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
using CodeAIExtension.Commands;
using CodeAIExtension.Services;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CodeAIExtension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [InstalledProductRegistration("Code AI", "AI assistant for coding", "1.0")]
    [ProvideToolWindow(typeof(AuthToolWindow))]
    [Guid(PackageGuids.CodeAIExtensionString)]
    [ProvideAppCommandLine("codeai", typeof(CodeAIExtensionPackage), Arguments = "1", DemandLoad = 1)]
    public sealed class CodeAIExtensionPackage : ToolkitPackage
    {
        internal static AuthService Auth { get; private set; }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            var cmdLine = (IVsAppCommandLine)await GetServiceAsync(typeof(SVsAppCommandLine));
            ErrorHandler.ThrowOnFailure(cmdLine.GetOption("codeai", out int present, out string value));
            if (present == 1 && !string.IsNullOrEmpty(value))
            {
                var s = 2;
            }

            Auth = new AuthService();
            await AuthToolWindowCommand.InitializeAsync(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Auth?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}