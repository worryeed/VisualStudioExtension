using CodeAI.Api.Contracts;
using CodeAI.Api.Data;
using CodeAI.Api.Models;
using MassTransit;

namespace CodeAI.Api.Services;

public sealed class CodeGenConsumer : IConsumer<CodeGenRequest>
{
    private readonly IAIService _ai;
    private readonly AppDbContext _db;
    private readonly IChatHistoryStore _chatHistoryStore;
    private readonly ILogger<CodeGenConsumer> _log;

    public CodeGenConsumer(IAIService ai, AppDbContext db, IChatHistoryStore chatHistoryStore, ILogger<CodeGenConsumer> log)
    {
        _ai = ai;
        _db = db;
        _chatHistoryStore = chatHistoryStore;
        _log = log;
    }

    public async Task Consume(ConsumeContext<CodeGenRequest> ctx)
    {
        var m = ctx.Message;
        _log.LogInformation("Consume {Kind} for user {User}", m.Kind, m.UserId);

        _ai.SetSettings(m.Temperature, m.MaxTokens);

        string code = m.Kind switch
        {
            CodeGenKind.Chat => await _ai.GenerateChatResponseAsync(m.Prompt, m.Context, m.Language, m.History!, ctx.CancellationToken),
            CodeGenKind.Docs => await _ai.GenerateXmlDocAsync(m.Prompt, m.Context, m.Language, ctx.CancellationToken),
            _ => await _ai.GenerateAutoCompleteAsync(m.Prompt, m.Context, m.Language, ctx.CancellationToken)
        };

        if (m.Kind == CodeGenKind.Chat)
        {
            m.History!.Add(new ChatMessage("AI bot", code));
            _chatHistoryStore.Set(m.UserId, m.History);
        }

        _db.QueryHistories.Add(new QueryHistory
        {
            Prompt = m.Prompt,
            Response = code,
            AppUserId = Guid.TryParse(m.UserId, out var id) ? id : (Guid?)null
        });

        await _db.SaveChangesAsync(ctx.CancellationToken);

        await ctx.RespondAsync(new CodeGenResult(m.RequestId, code, DateTime.UtcNow));
    }
}
