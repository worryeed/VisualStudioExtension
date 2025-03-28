using CodeAIExtension.Services;
using System.Threading.Tasks;

namespace CodeAIExtension;

[Command(PackageIds.CodeCommand)]
internal sealed class CodeCommand : BaseCommand<AddXmlDocCommand>
{
    //TODO
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        throw new NotImplementedException();
    }
}

[Command(PackageIds.ChatCommand)]
internal sealed class ChatCommand : BaseCommand<AddXmlDocCommand>
{
    //TODO
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        throw new NotImplementedException();
    }
}

[Command(PackageIds.AddXmlDocCommand)]
internal sealed class AddXmlDocCommand : BaseCommand<AddXmlDocCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        var text = await CodeEditorService.GetSelectedTextAsync();
        var xmlDocs = await ApiClient.GetXmlDocsAsync(text);
        await CodeEditorService.ReplaceSelectionAsync(xmlDocs);
    }
}
