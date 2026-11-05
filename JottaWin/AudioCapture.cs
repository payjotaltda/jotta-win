using System.IO;
using NAudio.Wave;

namespace JottaWin;

public sealed class AudioCapture : IDisposable
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _buffer;
    private WaveFormat? _format;

    public void Start()
    {
        Stop();
        _format = new WaveFormat(16000, 16, 1);
        _buffer = new MemoryStream();
        _waveIn = new WaveInEvent
        {
            WaveFormat = _format,
            BufferMilliseconds = 50,
        };
        _waveIn.DataAvailable += (_, e) => _buffer?.Write(e.Buffer, 0, e.BytesRecorded);
        _waveIn.StartRecording();
    }

    public byte[] StopToWav()
    {
        if (_waveIn is null) return Array.Empty<byte>();
        _waveIn.StopRecording();
        _waveIn.Dispose();
        _waveIn = null;
        var pcm = _buffer?.ToArray() ?? Array.Empty<byte>();
        _buffer?.Dispose();
        _buffer = null;
        if (pcm.Length < 800 || _format is null) return Array.Empty<byte>();
        using var outMs = new MemoryStream();
        using (var writer = new WaveFileWriter(outMs, _format))
        {
            writer.Write(pcm, 0, pcm.Length);
        }
        return outMs.ToArray();
    }

    public void StopDiscard()
    {
        if (_waveIn is not null)
        {
            _waveIn.StopRecording();
            _waveIn.Dispose();
            _waveIn = null;
        }
        _buffer?.Dispose();
        _buffer = null;
    }

    public static void PlayWav(byte[] wav)
    {
        using var ms = new MemoryStream(wav);
        using var reader = new WaveFileReader(ms);
        using var waveOut = new WaveOutEvent();
        using var ev = new ManualResetEventSlim(false);
        waveOut.Init(reader);
        waveOut.PlaybackStopped += (_, _) => ev.Set();
        waveOut.Play();
        ev.Wait(TimeSpan.FromMinutes(3));
    }

    public void Dispose() => StopDiscard();
}
