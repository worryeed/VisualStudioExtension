﻿namespace CodeAI.Api.Services;

public interface IAIService
{
    Task<string> GenerateCodeAsync(string prompt, string context, CancellationToken ct);
    Task<string> GenerateCodeForChatAsync(string prompt, string context, CancellationToken ct);
    Task<string> GenerateXmlDocAsync(string prompt, string context, CancellationToken ct);
}
