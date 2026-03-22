namespace TransVoice.Live.TextProcessing;

/// <summary>
/// Интерфейс для пост-обработки транскрибированного текста.
/// Реализации могут предоставлять правила для конкретных языков.
/// </summary>
public interface ITextProcessor
{
    string ProcessText(string text);
}
