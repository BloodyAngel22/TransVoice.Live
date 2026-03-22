using System.Diagnostics;

namespace TransVoice.Live.Infrastructure;

/// <summary>
/// Потоковый захват аудио с микрофона через утилиту arecord.
/// </summary>
public class AudioStreamer : IDisposable
{
    private Process? _process;

    public Stream StartStreaming()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "arecord",
            Arguments = "-D default -r 16000 -c 1 -f S16_LE -t raw",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        _process = new Process { StartInfo = startInfo };
        _process.Start();
        return _process.StandardOutput.BaseStream;
    }

    public void Stop()
    {
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill();
                _process.WaitForExit(500);
            }
            catch { }
            finally
            {
                _process.Dispose();
                _process = null;
            }
        }
    }

    public void Dispose() => Stop();
}
