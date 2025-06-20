﻿using CodeAI.Api.Models;
using System.Text;

namespace CodeAI.Api.Prompts;

public static class PromptFactory
{
    public static (string prompt, bool raw) AutoComplete(string prefix, string? ctx, string language)
    {
        var sb = new StringBuilder();
        sb.Append("<|fim_prefix|>")
          .Append("```" + language).Append('\n');
        if (!string.IsNullOrWhiteSpace(ctx)) sb.Append(ctx);
        sb.Append(prefix)
          .Append("<|fim_suffix|><|fim_middle|>");
        return (sb.ToString(), true);
    }

    public static string XmlDoc(string userPrompt, string? ctx, string language)
    {
        var sb = new StringBuilder();

        sb.AppendLine(
            $"В приведённом ниже фрагменте кода (на языке {language}) есть уровень декларации " +
            "(класс, метод, свойство или другой элемент). " +
            "Выбери самый верхний уровень декларации и сгенерируй **исключительно** XML-документацию " +
            "(теги `<summary>`, `<param>`, `<returns>` и т.п.) для этого элемента. " +
            "Никаких обёрток: ни ```xml```, ни `<?xml?>`, ни `<doc>`, ни `///`, ни дополнительного текста—только чистые XML-теги."
        );

        sb.AppendLine();
        sb.AppendLine("```" + language);
        sb.AppendLine(userPrompt.TrimEnd());
        sb.AppendLine("```");

        if (!string.IsNullOrWhiteSpace(ctx))
        {
            sb.AppendLine();
            sb.AppendLine("// Контекст всего файла:");
            sb.AppendLine("```" + language);
            sb.AppendLine(ctx.TrimEnd());
            sb.AppendLine("```");
        }

        return sb.ToString();
    }

    public static List<ChatMessage> Chat(string userPrompt, string? ctx, List<ChatMessage> history, string language)
    {
        if (history == null)
        {
            throw new ArgumentNullException(nameof(history), "Chat history cannot be null.");
        }

        if (history.Count == 0)
        {
            history.Add(new ChatMessage("system", $"You are an expert {language} coding assistant. Answer precisely and use idiomatic {language}."));
        }

        history.Add(new ChatMessage("user", BuildUserContent(userPrompt, ctx, language)));

        return history;
    }

    private static string BuildUserContent(string prompt, string? ctx, string language)
    {
        return $"{prompt}\n\r\n\rContext:\n\r```{language}\n\r{ctx}\n\r```";
    }
}