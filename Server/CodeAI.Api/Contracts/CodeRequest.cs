using System.ComponentModel.DataAnnotations;

namespace CodeAI.Api.Contracts;

public sealed class CodeRequest
{
    [Required, MinLength(4), MaxLength(2_048)]
    public string Prompt { get; init; } = default!;

    [MaxLength(100_000)]
    public string? Context { get; init; }

    [Required, RegularExpression(@"^[a-z0-9\+\#]+$",
              ErrorMessage = "Unexpected language id")]
    public string Language { get; init; } = "csharp";

    [Range(0, 1)]
    public double Temperature { get; init; } = 0.7;

    [Range(16, 4_096)]
    public int MaxTokens { get; init; } = 2_048;
}