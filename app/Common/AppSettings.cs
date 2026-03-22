namespace TransVoice.Live.Common;

/// <summary>
/// Модель настроек приложения: путь к модели, язык, потоки и статус конфигурации.
/// </summary>
public class AppSettings
{
    public string? ModelPath { get; set; }
    public string Language { get; set; } = "auto";
    public int Threads { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);
    public bool IsConfigured { get; set; } = false;
}
