using CodeAIExtension.Models;
using CodeAIExtension.Services;
using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System.ComponentModel.Design;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CodeAIExtension.Commands;

internal sealed class GenerateXmlDocCommand
{
    public const int CommandId = PackageIds.GenerateXmlDocCmd;
    public static readonly Guid CommandSet = PackageGuids.CodeAIExtension;

    private readonly AsyncPackage _package;

    private GenerateXmlDocCommand(AsyncPackage package, OleMenuCommandService mcs)
    {
        _package = package ?? throw new ArgumentNullException(nameof(package));

        var cmdId = new CommandID(CommandSet, CommandId);
        var cmd = new OleMenuCommand(Execute, cmdId);
        cmd.CommandChanged += UpdateVisibility;
        cmd.BeforeQueryStatus += UpdateVisibility;
        mcs.AddCommand(cmd);
    }

    public static async Task InitializeAsync(AsyncPackage package)
    {
        var mcs = await package.GetServiceAsync(typeof(IMenuCommandService))
                               as OleMenuCommandService;
        Assumes.Present(mcs);
        _ = new GenerateXmlDocCommand(package, mcs);
    }

    private void UpdateVisibility(object sender, EventArgs e)
    {

        if (sender is OleMenuCommand cmd)
        {
            cmd.Visible = cmd.Enabled = true;
        }
    }

    private void Execute(object sender, EventArgs e)
    {
        _ = _package.JoinableTaskFactory.RunAsync(async ( ) =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var textManager = (IVsTextManager)Package.GetGlobalService(typeof(SVsTextManager));
            if (textManager == null) return;

            textManager.GetActiveView(fMustHaveFocus: 1, null, ppView: out var vsView);
            if (vsView == null) return;

            vsView.GetBuffer(out var vsLines);
            if (vsLines == null) return;

            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var adapterFactory = componentModel.GetService<IVsEditorAdaptersFactoryService>();
            var textBuffer = adapterFactory.GetDocumentBuffer(vsLines as IVsTextBuffer);
            if (textBuffer == null) return;

            var wpfView = adapterFactory.GetWpfTextView(vsView);
            if (wpfView == null) return;

            var workspace = componentModel.GetService<VisualStudioWorkspace>();
            var container = textBuffer.AsTextContainer();
            var docId = workspace.GetDocumentIdInCurrentContext(container);
            if (docId == null) return;

            var rosDoc = workspace.CurrentSolution.GetDocument(docId);
            if (rosDoc == null) return;

            int caretPos = wpfView.Caret.Position.BufferPosition.Position;

            var root = await rosDoc.GetSyntaxRootAsync();

            var node = root.FindToken(caretPos).Parent?
                           .FirstAncestorOrSelf<MemberDeclarationSyntax>();

            if (node is not (ClassDeclarationSyntax or MethodDeclarationSyntax))
                return;

            if (node.GetLeadingTrivia()
                    .Any(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)))
                return;

            string prompt = node.ToFullString();
            var sourceText = await rosDoc.GetTextAsync();
            string context = sourceText.ToString();

            var request = new ChatRequest
            {
                Prompt = prompt,
                Context = context,
                Language = "csharp",
                Temperature = 0.7,
                MaxTokens = 1000
            };

            string xml;
            try
            {
                xml = await CallBackendAsync(request, default);
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory
                      .SwitchToMainThreadAsync();
                VsShellUtilities.ShowMessageBox(
                    _package, $"Ошибка генерации XML-дока: {ex.Message}",
                    "Generate XML Doc", OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }
            var lines = xml
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(l => l.TrimEnd());

            string leadingTriviaRaw = node.GetLeadingTrivia().ToFullString();

            string indent;
            int idxRn = leadingTriviaRaw.LastIndexOf("\r\n", StringComparison.Ordinal);
            int idxN = leadingTriviaRaw.LastIndexOf('\n');
            int splitPos = Math.Max(idxRn, idxN);
            if (splitPos >= 0)
            {
                int shift = (splitPos == idxRn ? 2 : 1);
                indent = leadingTriviaRaw.Substring(splitPos + shift);
            }
            else
            {
                indent = leadingTriviaRaw;
            }

            var sb = new StringBuilder();
            sb.Append(leadingTriviaRaw);
            int i = 0;
            foreach (var line in lines)
            {
                if (i != 0)
                {
                    sb.Append(indent);
                }
                sb.Append("/// ").AppendLine(line);
                i++;
            }

            sb.Append(indent);

            var xmlTrivia = SyntaxFactory.ParseLeadingTrivia(sb.ToString());
            var newNode = node.WithLeadingTrivia(xmlTrivia);
            var newRoot = root.ReplaceNode(node, newNode);
            var newSolution = rosDoc.Project.Solution.WithDocumentSyntaxRoot(rosDoc.Id, newRoot);
            workspace.TryApplyChanges(newSolution);
        });
    }

    private static async Task<string> CallBackendAsync(object dto, CancellationToken ct)
    {
        IAuthService authService = CodeAIExtensionPackage.Instance.Services.GetRequiredService<IAuthService>();
        IConfiguration cfg = CodeAIExtensionPackage.Instance.Services.GetRequiredService<IConfiguration>();
        if (!authService.IsAuthenticated)
        {
            throw new InvalidOperationException("Необходимо авторизоваться");
        }
        var baseAddress = cfg.GetSection("Auth:BaseUrl").Value;

        using var client = new HttpClient() { BaseAddress = new Uri(baseAddress) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authService.Jwt);
        var json = JsonSerializer.Serialize(dto);
        using var resp = await client.PostAsync(
            "/api/code/docs",
            new StringContent(json, Encoding.UTF8, "application/json"), ct);

        resp.EnsureSuccessStatusCode();
        var content = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        string code = root.GetProperty("code").GetString();
        return code;
    }
}