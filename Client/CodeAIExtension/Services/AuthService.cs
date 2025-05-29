#nullable enable

using Newtonsoft.Json;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace CodeAIExtension.Services;

public sealed class AuthService : IDisposable
{
    public bool IsAuthenticated => _jwt != null;
    public string? Jwt => _jwt;
    public event EventHandler? StateChanged;

    private readonly string _backend = "https://localhost:51155";
    private readonly HttpClient _http;
    private string? _jwt;
    private string? _refresh;
    private readonly string _tokenPath;
    private readonly CancellationTokenSource _cts = new();

    public AuthService()
    {
        _http = new HttpClient(new AuthHandler(this)) { BaseAddress = new Uri(_backend) };
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodeAI");
        Directory.CreateDirectory(dir);
        _tokenPath = Path.Combine(dir, "tokens.dat");
        LoadTokens();
        _ = Task.Run(ListenPipeAsync, _cts.Token);
        _ = Task.Run(RefreshLoopAsync, _cts.Token);
    }

    public void SignIn()
    {
        var url = $"{_backend}/api/auth/signin/github?redirectUri=codeai://callback";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }

    public async Task LogoutAsync()
    {
        try { await _http.PostAsync("/api/auth/logout", null); } catch { }
        ClearTokens();
        OnStateChanged();
    }

    private async Task ListenPipeAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                using var pipe = new NamedPipeServerStream(
                    "CodeAiAuth",
                    PipeDirection.In,
                    maxNumberOfServerInstances: 4,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(_cts.Token);

                using var reader = new StreamReader(pipe, Encoding.UTF8);
                var uriStr = await reader.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(uriStr))
                {
                    HandleUri(uriStr.Trim());
                }
            }
        }
        catch { }
    }


    private void HandleUri(string uriStr)
    {
        if (!Uri.TryCreate(uriStr, UriKind.Absolute, out var uri))
            return;

        var qs = HttpUtility.ParseQueryString(uri.Query);
        var jwt = qs["token"];
        var rt = qs["refresh"];

        if (string.IsNullOrEmpty(jwt) || string.IsNullOrEmpty(rt))
            return;
        _jwt = jwt;
        _refresh = rt;
        SaveTokens();

        OnStateChanged();
    }


    private async Task RefreshLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(12), _cts.Token).ContinueWith(_ => { }, TaskScheduler.Default);
            if (_cts.IsCancellationRequested) break;
            await RefreshAsync();
        }
    }

    private async Task<bool> RefreshAsync()
    {
        if (string.IsNullOrEmpty(_refresh)) return false;
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req.Headers.Add("Cookie", "refresh=" + _refresh);
        HttpResponseMessage rsp;
        try { rsp = await _http.SendAsync(req); }
        catch { return false; }
        if (!rsp.IsSuccessStatusCode) return false;
        var body = await rsp.Content.ReadAsStringAsync();
        var json = JsonConvert.DeserializeObject<TokenResponse>(body);
        if (json?.Token == null) return false;
        _jwt = json.Token;
        if (rsp.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            foreach (var c in cookies)
            {
                if (!c.StartsWith("refresh=", StringComparison.OrdinalIgnoreCase)) continue;
                var semicolon = c.IndexOf(';');
                _refresh = semicolon >= 0 ? c.Substring(8, semicolon - 8) : c.Substring(8);
                break;
            }
        }
        SaveTokens();
        OnStateChanged();
        return true;
    }

    private void LoadTokens()
    {
        if (!File.Exists(_tokenPath)) return;
        try
        {
            var protectedBytes = File.ReadAllBytes(_tokenPath);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(bytes);
            var pair = JsonConvert.DeserializeObject<TokenPair>(json);
            _jwt = pair?.Access;
            _refresh = pair?.Refresh;
        }
        catch { ClearTokens(); }
    }

    private void SaveTokens()
    {
        if (string.IsNullOrEmpty(_jwt) || string.IsNullOrEmpty(_refresh)) return;
        var json = JsonConvert.SerializeObject(new TokenPair { Access = _jwt, Refresh = _refresh });
        var bytes = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_tokenPath, protectedBytes);
    }

    private void ClearTokens()
    {
        _jwt = _refresh = null;
        try { if (File.Exists(_tokenPath)) File.Delete(_tokenPath); } catch { }
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose() => _cts.Cancel();

    private class TokenPair
    {
        [JsonProperty("access")] public string? Access { get; set; }
        [JsonProperty("refresh")] public string? Refresh { get; set; }
    }

    private class TokenResponse
    {
        [JsonProperty("token")] public string? Token { get; set; }
    }

    private sealed class AuthHandler : DelegatingHandler
    {
        private readonly AuthService _svc;
        public AuthHandler(AuthService svc) => _svc = svc;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken t)
        {
            if (_svc._jwt != null) r.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _svc._jwt);
            var rsp = await base.SendAsync(r, t);
            if (rsp.StatusCode == HttpStatusCode.Unauthorized && await _svc.RefreshAsync())
            {
                r.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _svc._jwt);
                rsp.Dispose();
                rsp = await base.SendAsync(r, t);
            }
            return rsp;
        }
    }
}