using CodeAIExtension.Models;
using Markdig;
using Markdig.Syntax;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CodeAIExtension.ViewModels;

public class ChatMessageViewModel
{
    public ChatMessage Model { get; }
    public ObservableCollection<MarkdownSegment> Segments { get; }

    public ChatMessageViewModel(ChatMessage msg)
    {
        Model = msg;
        Segments = ParseMarkdown(msg.Content);
    }

    public static ObservableCollection<MarkdownSegment> ParseMarkdown(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return new ObservableCollection<MarkdownSegment>
            {
                new TextSegment(string.Empty)
            };
        }    

        var pipeline = new MarkdownPipelineBuilder()
            .Build();

        var document = Markdown.Parse(markdown, pipeline);
        var list = new ObservableCollection<MarkdownSegment>();

        foreach (var block in document)
        {
            switch (block)
            {
                case FencedCodeBlock fcb:
                    {
                        var lang = fcb.Info ?? string.Empty;
                        var code = fcb.Lines.ToString() ?? string.Empty;
                        list.Add(new CodeSegment(lang, code));
                        break;
                    }
                case ParagraphBlock para:
                    {
                        var text = para.Inline?.FirstChild?.ToString()?.Trim() ?? para.ToString().Trim();
                        list.Add(new TextSegment(text));
                        break;
                    }
                default:
                    {
                        var text = block.ToString().Trim();
                        list.Add(new TextSegment(text));
                        break;
                    }
            }
        }

        if (!list.Any())
            list.Add(new TextSegment(markdown.Trim()));

        return list;
    }
}
