using TransVoice.Live.Common;
using Whisper.net;

namespace TransVoice.Live.Core;

/// <summary>
/// Обёртка над Whisper.net. Загружает модель и создаёт процессоры для распознавания речи.
/// </summary>
public class WhisperEngine : IDisposable
{
    private WhisperFactory? _factory;
    private readonly AppSettings _settings;

    public WhisperEngine(AppSettings settings)
    {
        _settings = settings;
    }

    private void EnsureFactory()
    {
        if (_factory == null)
        {
            if (string.IsNullOrEmpty(_settings.ModelPath) || !File.Exists(_settings.ModelPath))
                throw new FileNotFoundException($"Файл модели не найден: {_settings.ModelPath}");

            _factory = WhisperFactory.FromPath(_settings.ModelPath);
        }
    }

    public WhisperProcessorBuilder GetProcessorBuilder()
    {
        EnsureFactory();
        return _factory!
            .CreateBuilder()
            .WithLanguage(_settings.Language)
            .WithThreads(_settings.Threads);
    }

    public void Dispose()
    {
        _factory?.Dispose();
    }
}
