using System.Text.RegularExpressions;

namespace JottaWin;

public sealed class WakeListenService : IDisposable
{
    private static readonly Regex WakeRe = new(
        @"\b(jotta|jota|iota|giota|yotta|yota|hota|djota)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly Func<JottaApi?> _apiFactory;
    private readonly Action _onWake;
    private readonly AudioCapture _mic = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public WakeListenService(Func<JottaApi?> apiFactory, Action onWake)
    {
        _apiFactory = apiFactory;
        _onWake = onWake;
    }

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _loop = null;
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1200, ct);
                var api = _apiFactory();
                if (api is null || !await api.PingAsync(ct))
                    continue;

                _mic.Start();
                await Task.Delay(2200, ct);
                var wav = _mic.StopToWav();
                if (wav.Length < 800) continue;

                var text = await api.TranscribeAsync(wav, wakePrompt: true, ct);
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (!WakeRe.IsMatch(text)) continue;
                _onWake();
                await Task.Delay(3500, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(2000, ct);
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _mic.Dispose();
    }
}
