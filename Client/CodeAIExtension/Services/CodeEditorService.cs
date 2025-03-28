using System.Threading.Tasks;

namespace CodeAIExtension.Services;

public static class CodeEditorService
{
    public static async Task<string> GetSelectedTextAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var docView = await VS.Documents.GetActiveDocumentViewAsync();
        return docView?.TextView.Selection.StreamSelectionSpan.GetText();
    }

    public static async Task ReplaceSelectionAsync(string newCode)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var docView = await VS.Documents.GetActiveDocumentViewAsync();
        var selection = docView?.TextView.Selection;

        if (selection != null)
        {
            docView.TextBuffer.Replace(selection.StreamSelectionSpan.SnapshotSpan, newCode);
        }
    }
}
