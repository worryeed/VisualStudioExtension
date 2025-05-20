using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace CodeAI.Api.Services;

public interface IRedisCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
}

public class RedisCacheService : IRedisCacheService
{
    private readonly IDistributedCache _cache;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public RedisCacheService(IDistributedCache cache) => _cache = cache;

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var bytes = await _cache.GetAsync(key, ct);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes, _json);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(value, _json);
        var opts = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
        await _cache.SetAsync(key, data, opts, ct);
    }
}
