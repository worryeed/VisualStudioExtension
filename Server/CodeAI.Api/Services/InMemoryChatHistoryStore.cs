using CodeAI.Api.Models;
using System.Collections.Concurrent;

namespace CodeAI.Api.Services;

public interface IChatHistoryStore
{
    void Set(string userId, List<ChatMessage> history);
    List<ChatMessage>? Get(string userId);
    void Clear(string userId);          
}

public sealed class InMemoryChatHistoryStore : IChatHistoryStore
{
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _mem
        = new();

    public void Set(string userId, List<ChatMessage> history) =>
        _mem.AddOrUpdate(userId, history, (_, _) => history);

    public List<ChatMessage>? Get(string userId) =>
        _mem.TryGetValue(userId, out var h) ? h : null;

    public void Clear(string userId) => _mem.TryRemove(userId, out _);
}
