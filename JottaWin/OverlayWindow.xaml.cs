using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace JottaWin;

public partial class OverlayWindow : Window
{
    private readonly JottaApi _api;
    private readonly AudioCapture _mic = new();
    private bool _recording;
    public event Action? ClosedByUser;

    public OverlayWindow(JottaApi api)
    {
        _api = api;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            DwmHelper.TryEnableBlur(this);
            StartOrbPulse();
            ReplyPreview.Text = "JOTTA · Ctrl+Espaço chat · Esc sai · segure o orbe para falar";
        };
        Closed += (_, _) => ClosedByUser?.Invoke();
    }

    public void SetState(string label) => StateLabel.Text = label;

    public void SetReply(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            ReplyPreview.Text = text;
    }

    private void StartOrbPulse()
    {
        OrbCore.RenderTransform = new ScaleTransform(1, 1);
        OrbCore.RenderTransformOrigin = new Point(0.5, 0.5);
        var anim = new DoubleAnimation(0.94, 1.06, TimeSpan.FromSeconds(1.35))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase(),
        };
        OrbCore.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        OrbCore.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    public void OpenChat()
    {
        ChatPanel.Visibility = Visibility.Visible;
        ChatInput.Clear();
        ChatInput.Focus();
    }

    public void CloseChat() => ChatPanel.Visibility = Visibility.Collapsed;

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (ChatPanel.Visibility == Visibility.Visible)
            {
                CloseChat();
                e.Handled = true;
                return;
            }
            Hide();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            if (ChatPanel.Visibility == Visibility.Visible)
                ChatInput.Focus();
            else
                OpenChat();
        }
    }

    private void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            _ = SendChatAsync();
        }
    }

    private async void SendChat_Click(object sender, RoutedEventArgs e) => await SendChatAsync();

    private void CloseChat_Click(object sender, RoutedEventArgs e) => CloseChat();

    private async Task SendChatAsync()
    {
        var text = ChatInput.Text.Trim();
        if (text.Length == 0) return;
        CloseChat();
        try
        {
            SetState("enviando…");
            await _api.SendTextAsync(text);
            SetReply("Enviado ao Cursor no storage-dev…");
        }
        catch (Exception ex)
        {
            SetReply(ex.Message);
        }
        finally
        {
            SetState("pronto");
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (ChatPanel.Visibility == Visibility.Visible) return;
        _ = ToggleRecordAsync();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_recording)
            _ = StopRecordAndSendAsync();
    }

    private Task ToggleRecordAsync()
    {
        if (_recording) return Task.CompletedTask;
        _recording = true;
        SetState("gravando");
        try
        {
            _mic.Start();
        }
        catch (Exception ex)
        {
            _recording = false;
            SetReply($"Microfone: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    private async Task StopRecordAndSendAsync()
    {
        if (!_recording) return;
        _recording = false;
        var wav = _mic.StopToWav();
        if (wav.Length < 400)
        {
            SetReply("Gravação curta — segure o orbe e fale.");
            SetState("pronto");
            return;
        }
        try
        {
            SetState("transcrevendo…");
            var text = await _api.TranscribeAsync(wav, wakePrompt: false);
            if (string.IsNullOrWhiteSpace(text))
            {
                SetReply("Não entendi.");
                return;
            }
            SetReply($"Ouvi: {text}");
            SetState("enviando…");
            await _api.SendTextAsync(text);
        }
        catch (Exception ex)
        {
            SetReply(ex.Message);
        }
        finally
        {
            SetState("pronto");
        }
    }
}
