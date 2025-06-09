using CodeAIExtension.ViewModels;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Composition;
using System.Net.Http;
using System.Threading.Tasks;

namespace CodeAIExtension;

[ExportCompletionProvider(nameof(AICompletionProvider), LanguageNames.CSharp)]
[Shared]
public class AICompletionProvider : CompletionProvider
{
    private static readonly HttpClient httpClient = new HttpClient();

    public override async Task ProvideCompletionsAsync(CompletionContext context)
    {
        var cfg = CodeAIExtensionPackage.Instance.Services.GetRequiredService<IConfiguration>();
        var backend = cfg.GetSection("Auth:BaseUrl").Value!;

        var documentText = await context.Document.GetTextAsync();
        var beforeCursor = documentText.ToString().Substring(0, context.Position);

        var response = await httpClient.PostAsync(
            backend + "/api/code/autoComplete",
            new StringContent(beforeCursor)
        );

        if (response.IsSuccessStatusCode)
        {
            var aiSuggestion = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(aiSuggestion))
            {
                context.AddItem(CompletionItem.Create(aiSuggestion));
            }
        }
    }
}