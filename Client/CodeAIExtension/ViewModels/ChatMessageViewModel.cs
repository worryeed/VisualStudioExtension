using CodeAIExtension.Models;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
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
        if (string.IsNullOrWhiteSpace(markdown))
            return new ObservableCollection<MarkdownSegment> { new TextSegment(string.Empty) };

        var pipeline = new MarkdownPipelineBuilder().Build();
        var document = Markdown.Parse(markdown, pipeline);
        var segments = new ObservableCollection<MarkdownSegment>();

        foreach (var block in document)
        {
            switch (block)
            {
                case FencedCodeBlock fcb:
                    segments.Add(new CodeSegment(fcb.Info ?? string.Empty, fcb.Lines.ToString()));
                    break;

                case ParagraphBlock p:
                    segments.Add(new TextSegment(InlineText(p.Inline)));
                    break;

                case HeadingBlock h:
                    segments.Add(new TextSegment(InlineText(h.Inline)));
                    break;

                case ListBlock l:
                    foreach (var item in l.OfType<ListItemBlock>())
                    {
                        foreach (var sub in item.OfType<ParagraphBlock>())
                            segments.Add(new TextSegment(InlineText(sub.Inline)));
                    }
                    break;

                default:
                    var leaf = block as LeafBlock;
                    if (leaf?.Inline != null)
                        segments.Add(new TextSegment(InlineText(leaf.Inline)));
                    break;
            }
        }

        if (!segments.Any())
            segments.Add(new TextSegment(markdown.Trim()));

        return segments;
    }

    static string InlineText(ContainerInline? inline)
    {
        if (inline == null) return string.Empty;
        var sb = new StringBuilder();
        foreach (var lit in inline.Descendants().OfType<LiteralInline>())
            sb.Append(lit.Content.Text, lit.Content.Start, lit.Content.Length);
        return sb.ToString().Trim();
    }
}
