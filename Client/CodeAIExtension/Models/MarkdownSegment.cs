using CodeAIExtension.Services;
using System.Windows;
using System.Windows.Input;

namespace CodeAIExtension.Models;

public abstract class MarkdownSegment { }

public class TextSegment : MarkdownSegment
{
    public string Text { get; set; }
    public TextSegment(string text) => Text = text;
}

public class CodeSegment : MarkdownSegment
{
    public string Language { get; set; }
    public string Code { get; set;  }
    public ICommand CopyCommand { get; set; }

    public CodeSegment(string lang, string code)
    {
        Language = lang;
        Code = code;
        CopyCommand = new AsyncRelayCommand(async () => Clipboard.SetText(Code));
    }
}
