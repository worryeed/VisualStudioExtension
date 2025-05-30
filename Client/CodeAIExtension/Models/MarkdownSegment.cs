using CodeAIExtension.Services;
using System.Windows;
using System.Windows.Input;

namespace CodeAIExtension.Models;

public abstract class MarkdownSegment { }

public class TextSegment : MarkdownSegment
{
    public string Text { get; }
    public TextSegment(string text) => Text = text;
}

public class CodeSegment : MarkdownSegment
{
    public string Language { get; }
    public string Code { get; }
    public ICommand CopyCommand { get; }

    public CodeSegment(string lang, string code)
    {
        Language = lang;
        Code = code;
        CopyCommand = new AsyncRelayCommand(async () => Clipboard.SetText(Code));
    }
}
