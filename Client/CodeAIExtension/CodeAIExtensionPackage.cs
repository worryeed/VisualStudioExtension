global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
using CodeAIExtension.Commands;
using CodeAIExtension.Services;
using CodeAIExtension.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Shell.Interop;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CodeAIExtension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [InstalledProductRegistration("Code AI", "AI assistant for coding", "1.0")]
    [ProvideToolWindow(typeof(AuthToolWindow))]
    [ProvideToolWindow(typeof(ChatToolWindow))]
    [Guid(PackageGuids.CodeAIExtensionString)]
    [ProvideAppCommandLine("codeai", typeof(CodeAIExtensionPackage), Arguments = "1", DemandLoad = 1)]
    public sealed class CodeAIExtensionPackage : ToolkitPackage
    {
        public IServiceProvider Services { get; private set; }
        public static CodeAIExtensionPackage Instance { get; private set; }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            Instance = this;
            var services = new ServiceCollection();
            ConfigureServices(services);
            AddJson(services);
            Services = services.BuildServiceProvider();

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await AuthToolWindowCommand.InitializeAsync(this);
            await ChatToolWindowCommand.InitializeAsync(this);

            await base.InitializeAsync(cancellationToken, progress);
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<IChatService, ChatService>();
            services.AddSingleton<AuthToolWindowViewModel>();
            services.AddSingleton<ChatToolWindowViewModel>();
        }

        private void AddJson(IServiceCollection services)
        {
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var config = new ConfigurationBuilder()
                .SetBasePath(assemblyLocation)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            services.AddSingleton<IConfiguration>(config);
        }
    }
}