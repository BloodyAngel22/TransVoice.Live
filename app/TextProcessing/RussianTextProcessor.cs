using System.Text;
using System.Text.RegularExpressions;

namespace TransVoice.Live.TextProcessing;

/// <summary>
/// Пост-обработка транскрибированного текста для русского языка.
/// Исправляет пробелы, знаки препинания, заглавные буквы, тире, кавычки и переносы строк.
/// </summary>
public class RussianTextProcessor : ITextProcessor
{
    // Пробелы: несколько пробелов/табов → один пробел
    private static readonly Regex _multipleSpaces = new(@"[ \t]{2,}", RegexOptions.Compiled);

    // Пробел перед знаком препинания → убрать
    private static readonly Regex _spaceBefore = new(@" +([,\.!?:;…\)\]»])", RegexOptions.Compiled);

    // Нет пробела после знака препинания (если дальше буква/цифра) → добавить
    private static readonly Regex _noSpaceAfter = new(
        @"([,\.!?:;…])([^\s\d\.\!\?,;:…\)\]»\n])",
        RegexOptions.Compiled
    );

    // Пробел после открывающей скобки/кавычки → убрать
    private static readonly Regex _spaceAfterOpen = new(@"([\(\[«]) +", RegexOptions.Compiled);

    // Многозначие: 2+ точки (не многоточие) → многоточие
    private static readonly Regex _doubleDot = new(@"\.{2,}", RegexOptions.Compiled);

    // Многозначие: 2+ восклицательных → один
    private static readonly Regex _multiExclamation = new(@"!{2,}", RegexOptions.Compiled);

    // Многозначие: 2+ вопросительных → один
    private static readonly Regex _multiQuestion = new(@"\?{2,}", RegexOptions.Compiled);

    // Прямые кавычки → русские «»
    //    Открывающая: " перед словом (нет пробела справа)
    private static readonly Regex _openQuote = new(@"""(?=\S)", RegexOptions.Compiled);

    // Закрывающая: " после слова (нет пробела слева)
    private static readonly Regex _closeQuote = new(@"(?<=\S)""", RegexOptions.Compiled);

    // Дефис между словами с пробелами → тире (—)
    //    Шаблон: слово/пробел + дефис + пробел/слово
    private static readonly Regex _hyphenToEmDash = new(@"(?<=\S) - (?=\S)", RegexOptions.Compiled);

    // Точка между словами без знака препинания
    //      Строчная буква → пробел → Заглавная (только если нет .!?) перед)
    private static readonly Regex _missingPeriod = new(
        @"([а-яёa-z])(\s+)([А-ЯЁA-Z])",
        RegexOptions.Compiled
    );

    // Дублирующиеся точки (.., . ., .  .) → одна точка
    private static readonly Regex _duplicateDots = new(@"\.\s*\.", RegexOptions.Compiled);

    // Тире в начале строки (прямая речь / список)
    private static readonly Regex _leadingHyphen = new(
        @"^- ",
        RegexOptions.Compiled | RegexOptions.Multiline
    );

    // Заглавная буква в начале предложения (после . ! ? … и переноса строки)
    // Паттерн: точка/восклицание/вопрос + пробел(ы) + строчная буква
    private static readonly Regex _capitalizeAfterPunct = new(
        @"([\.!?…]\s+)([а-яёa-z])",
        RegexOptions.Compiled
    );

    // Заглавная буква в начале каждой строки (после \n)
    private static readonly Regex _capitalizeAfterNewline = new(
        @"((?:^|\n)\s*)([а-яёa-z])",
        RegexOptions.Compiled
    );

    // Пробел перед переносом строки → убрать
    private static readonly Regex _spaceBeforeNewline = new(@" +\n", RegexOptions.Compiled);

    // Три и более переносов подряд → два (один пустая строка)
    private static readonly Regex _multipleNewlines = new(@"\n{3,}", RegexOptions.Compiled);

    // Точка в конце строки — если строка не заканчивается на знак препинания, добавить точку
    // Применяется к последней строке текста
    private static readonly Regex _missingFinalPunct = new(
        @"([^\.!?\…\n])$",
        RegexOptions.Compiled
    );

    public string ProcessText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        text = _doubleDot.Replace(text, "...");
        text = _multiExclamation.Replace(text, "!");
        text = _multiQuestion.Replace(text, "?");

        text = _multipleSpaces.Replace(text, " ");
        text = _spaceBefore.Replace(text, "$1");
        text = _spaceAfterOpen.Replace(text, "$1");
        text = _noSpaceAfter.Replace(text, "$1 $2");
        text = _spaceBeforeNewline.Replace(text, "\n");

        text = _multipleNewlines.Replace(text, "\n\n");

        text = _openQuote.Replace(text, "«");
        text = _closeQuote.Replace(text, "»");

        text = _hyphenToEmDash.Replace(text, " — ");
        text = _leadingHyphen.Replace(text, "— ");

        text = _duplicateDots.Replace(text, ".");

        text = _missingPeriod.Replace(text, "$1.$2");

        text = _capitalizeAfterPunct.Replace(
            text,
            m => m.Groups[1].Value + char.ToUpper(m.Groups[2].Value[0]).ToString()
        );
        text = _capitalizeAfterNewline.Replace(
            text,
            m => m.Groups[1].Value + char.ToUpper(m.Groups[2].Value[0]).ToString()
        );

        text = CapitalizeFirstChar(text);

        var trimmed = text.TrimEnd();
        if (trimmed.Length > 0 && !Regex.IsMatch(trimmed, @"[\.!?\…»\)]$"))
        {
            text = trimmed + ".";
        }
        else
        {
            text = trimmed;
        }

        return text;
    }

    private static string CapitalizeFirstChar(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsLetter(text[i]))
            {
                if (char.IsUpper(text[i]))
                    return text;

                var sb = new StringBuilder(text);
                sb[i] = char.ToUpper(text[i]);
                return sb.ToString();
            }
        }
        return text;
    }
}
