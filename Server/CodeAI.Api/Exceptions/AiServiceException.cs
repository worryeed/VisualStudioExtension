namespace CodeAI.Api.Exceptions;

public class AiServiceException : Exception
{
    public AiServiceException(string message) : base(message) { }
}
