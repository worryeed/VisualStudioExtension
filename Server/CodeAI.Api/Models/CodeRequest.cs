using System.ComponentModel.DataAnnotations;

namespace CodeAI.Api.Models;

public record CodeRequest(
    [Required(ErrorMessage = "Prompt is required")]
    string Prompt,

    string Context = "");