using CodeAI.Api.Contracts;
using CodeAI.Api.Data;
using CodeAI.Api.Models;
using MassTransit;

namespace CodeAI.Api.Services;

public sealed class CodeGenConsumer : IConsumer<CodeGenRequest>
{
    private readonly IAIService _ai;
    private readonly AppDbContext _db;

    public CodeGenConsumer(IAIService ai, AppDbContext db)
    {
        _ai = ai;
        _db = db;
    }

    public async Task Consume(ConsumeContext<CodeGenRequest> ctx)
    {
        var m = ctx.Message;

        var code = await _ai.GenerateCodeAsync(
            m.Prompt, m.Context, m.Language, ctx.CancellationToken);

        Guid? userGuid = null;
        if (Guid.TryParse(m.UserId, out var g))
            userGuid = g;

        _db.QueryHistories.Add(new QueryHistory
        {
            Prompt = m.Prompt,
            Response = code,
            AppUserId = userGuid
        });
        await _db.SaveChangesAsync(ctx.CancellationToken);

        await ctx.RespondAsync(new CodeGenResult(
            m.RequestId, code, DateTime.UtcNow));
    }
}