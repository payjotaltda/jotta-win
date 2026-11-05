using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace JottaWin;

public sealed class JottaSession
{
    public string State { get; set; } = "idle";
    public bool OrchestratorLive { get; set; }
    public string LastReplyPreview { get; set; } = "";
    public JottaTts? Tts { get; set; }
}

public sealed class JottaTts
{
    public bool Ready { get; set; }
    public int Token { get; set; }
    public string? Url { get; set; }
    public double DurationSec { get; set; }
    public int AudibleToken { get; set; }
    public bool Playing { get; set; }
}

public sealed class JottaApi
{
    private readonly HttpClient _http;

    public JottaApi(string baseUrl)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        _http = new HttpClient { BaseAddress = new Uri(trimmed + "/") };
        _http.DefaultRequestHeaders.Add("X-Jotta-Client", "win");
        _http.Timeout = TimeSpan.FromSeconds(120);
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _http.GetAsync("voice-orb/session.json", ct);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<JottaSession?> GetSessionAsync(CancellationToken ct = default)
    {
        var res = await _http.GetAsync("voice-orb/session.json", ct);
        if (!res.IsSuccessStatusCode) return null;
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;
        var ttsEl = root.TryGetProperty("tts", out var t) ? t : default;
        JottaTts? tts = null;
        if (ttsEl.ValueKind == JsonValueKind.Object)
        {
            tts = new JottaTts
            {
                Ready = ttsEl.TryGetProperty("ready", out var r) && r.GetBoolean(),
                Token = ttsEl.TryGetProperty("token", out var tok) ? tok.GetInt32() : 0,
                Url = ttsEl.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String
                    ? u.GetString()
                    : null,
                DurationSec = ttsEl.TryGetProperty("duration_sec", out var d) ? d.GetDouble() : 0,
                AudibleToken = ttsEl.TryGetProperty("audible_token", out var a) ? a.GetInt32() : 0,
                Playing = ttsEl.TryGetProperty("playing", out var p) && p.GetBoolean(),
            };
        }
        var orch = root.TryGetProperty("orchestrator", out var o) ? o : default;
        var live = orch.ValueKind == JsonValueKind.Object
                   && orch.TryGetProperty("live", out var lv)
                   && lv.GetBoolean();
        return new JottaSession
        {
            State = root.TryGetProperty("state", out var st) ? st.GetString() ?? "idle" : "idle",
            OrchestratorLive = live,
            LastReplyPreview = root.TryGetProperty("last_reply_preview", out var lr)
                ? lr.GetString() ?? ""
                : "",
            Tts = tts,
        };
    }

    public async Task SendTextAsync(string text, CancellationToken ct = default)
    {
        var payload = new { text };
        var res = await _http.PostAsJsonAsync("voice-orb/utterance", payload, ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task<string> TranscribeAsync(byte[] wavBytes, bool wakePrompt, CancellationToken ct = default)
    {
        using var content = new ByteArrayContent(wavBytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        using var req = new HttpRequestMessage(HttpMethod.Post, "voice-orb/transcribe") { Content = content };
        if (wakePrompt)
            req.Headers.Add("X-Voice-Orb-Stt", "wake");
        var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;
        if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
        {
            var err = root.TryGetProperty("error", out var e) ? e.GetString() : "transcribe failed";
            throw new InvalidOperationException(err ?? "transcribe failed");
        }
        return root.TryGetProperty("text", out var tx) ? (tx.GetString() ?? "").Trim() : "";
    }

    public async Task<byte[]> DownloadTtsAsync(int token, CancellationToken ct = default)
    {
        var res = await _http.GetAsync($"voice-orb/tts/latest.wav?t={token}", ct);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task MarkTtsAudibleAsync(int token, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(new { token });
        var res = await _http.PostAsync(
            "voice-orb/tts/audible",
            new StringContent(json, Encoding.UTF8, "application/json"),
            ct);
        res.EnsureSuccessStatusCode();
    }
}
