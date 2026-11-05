using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace JottaWin;

public sealed class AppHost : IDisposable
{
    private const string SettingsFile = "jotta-settings.json";
    private readonly NotifyIcon _tray;
    private JottaApi? _api;
    private OverlayWindow? _overlay;
    private WakeListenService? _wake;
    private MainWindow? _settings;
    private SessionPoller? _poller;

    public AppHost()
    {
        _tray = new NotifyIcon
        {
            Text = "JOTTA",
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir JOTTA (Ctrl+Alt+J)", null, (_, _) => ShowOverlay());
        menu.Items.Add("Configurações…", null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowOverlay();
    }

    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JOTTA",
            SettingsFile);

    public async Task StartAsync()
    {
        await LoadApiAsync();
        _wake = new WakeListenService(() => _api, ShowOverlay);
        _wake.Start();
        _poller = new SessionPoller(() => _api, OnSession);
        _poller.Start();
    }

    private async Task LoadApiAsync()
    {
        var url = "https://storage-dev.tailf8d872.ts.net:8443";
        try
        {
            if (File.Exists(SettingsPath))
            {
                using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(SettingsPath));
                if (doc.RootElement.TryGetProperty("serverUrl", out var u))
                    url = u.GetString() ?? url;
            }
        }
        catch { /* default */ }

        _api = new JottaApi(url);
    }

    private void OnSession(JottaSession session)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_overlay is null || !_overlay.IsVisible) return;
            _overlay.SetState(MapState(session.State));
            if (!string.IsNullOrWhiteSpace(session.LastReplyPreview))
                _overlay.SetReply(session.LastReplyPreview);
            var tts = session.Tts;
            if (tts is { Ready: true, Token: > 0 } && tts.AudibleToken < tts.Token)
                _ = PlayTtsAsync(tts.Token);
        });
    }

    private async Task PlayTtsAsync(int token)
    {
        if (_api is null) return;
        try
        {
            var wav = await _api.DownloadTtsAsync(token);
            await Task.Run(() => AudioCapture.PlayWav(wav));
            await _api.MarkTtsAudibleAsync(token);
        }
        catch
        {
            /* ignore */
        }
    }

    private static string MapState(string s) => s switch
    {
        "idle" => "pronto",
        "listening" => "ouvindo",
        "thinking" => "pensando",
        "responding" => "respondendo",
        "waiting_cursor" => "cursor",
        _ => s,
    };

    private void ShowOverlay()
    {
        if (_api is null) return;
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_overlay is null)
            {
                _overlay = new OverlayWindow(_api);
                _overlay.ClosedByUser += () => _overlay = null;
            }
            if (!_overlay.IsVisible)
            {
                _overlay.Show();
                _overlay.Activate();
            }
        });
    }

    private void ShowSettings()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _settings ??= new MainWindow(onSaved: async url =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                await File.WriteAllTextAsync(
                    SettingsPath,
                    JsonSerializer.Serialize(new { serverUrl = url.Trim().TrimEnd('/') }));
                _api = new JottaApi(url);
            });
            _settings.Show();
            _settings.Activate();
        });
    }

    private void ExitApp()
    {
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _wake?.Dispose();
        _poller?.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
    }
}

internal sealed class SessionPoller : IDisposable
{
    private readonly Func<JottaApi?> _api;
    private readonly Action<JottaSession> _onSession;
    private CancellationTokenSource? _cts;
    private int _lastTts;

    public SessionPoller(Func<JottaApi?> api, Action<JottaSession> onSession)
    {
        _api = api;
        _onSession = onSession;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = Loop(_cts.Token);
    }

    private async Task Loop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var api = _api();
                if (api is not null)
                {
                    var s = await api.GetSessionAsync(ct);
                    if (s is not null)
                    {
                        if (s.Tts is { Ready: true, Token: > 0 } t && t.Token > _lastTts)
                            _lastTts = t.Token;
                        _onSession(s);
                    }
                }
            }
            catch { /* ignore */ }

            await Task.Delay(450, ct);
        }
    }

    public void Dispose() => _cts?.Cancel();
}
