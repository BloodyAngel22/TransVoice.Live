using System.Security.Cryptography;
using Spectre.Console;
using TransVoice.Live.Common;

namespace TransVoice.Live.Infrastructure;

/// <summary>
/// Информация об одной модели Whisper доступной для скачивания.
/// </summary>
public record WhisperModelInfo(
    string Name,
    string DisplayName,
    string Description,
    string SizeHuman,
    long SizeBytes,
    string Sha1Hash,
    string DownloadUrl
);

/// <summary>
/// Скачивает GGML-модели Whisper с Hugging Face и проверяет целостность по SHA-1.
/// </summary>
public class ModelDownloader
{
    private const string HuggingFaceBase =
        "https://huggingface.co/ggerganov/whisper.cpp/resolve/main";

    /// <summary>
    /// Список официальных моделей Whisper в GGML-формате.
    /// SHA-1 хэши взяты с https://huggingface.co/ggerganov/whisper.cpp
    /// </summary>
    public static readonly IReadOnlyList<WhisperModelInfo> AvailableModels =
    [
        new(
            "tiny",
            "Tiny",
            "Быстрая, минимальные ресурсы",
            "75 МБ",
            78_741_440,
            "bd577a113a864445d4c299885e0cb97d4ba92b5f",
            $"{HuggingFaceBase}/ggml-tiny.bin"
        ),
        new(
            "base",
            "Base",
            "Баланс скорости и точности",
            "142 МБ",
            148_897_792,
            "465707469ff3a37a2b9b8d8f89f2f99de7299dac",
            $"{HuggingFaceBase}/ggml-base.bin"
        ),
        new(
            "small",
            "Small",
            "Хорошая точность, умеренные ресурсы",
            "466 МБ",
            488_636_416,
            "55356645c2b361a969dfd0ef2c5a50d530afd8d5",
            $"{HuggingFaceBase}/ggml-small.bin"
        ),
        new(
            "medium",
            "Medium",
            "Высокая точность",
            "1.5 ГБ",
            1_533_000_000,
            "fd9727b6e1217c2f614f9b698455c4ffd82463b4",
            $"{HuggingFaceBase}/ggml-medium.bin"
        ),
        new(
            "large-v1",
            "Large v1",
            "Максимальная точность (v1)",
            "2.9 ГБ",
            3_094_000_000,
            "b1caaf735c4cc1429223d5a74f0f4d0b9b59a299",
            $"{HuggingFaceBase}/ggml-large-v1.bin"
        ),
        new(
            "large-v2",
            "Large v2",
            "Максимальная точность (v2)",
            "2.9 ГБ",
            3_094_000_000,
            "0f4c8e34f21cf1a914c59d8b3ce882345ad349d6",
            $"{HuggingFaceBase}/ggml-large-v2.bin"
        ),
        new(
            "large-v3",
            "Large v3",
            "Максимальная точность (v3)",
            "2.9 ГБ",
            3_094_000_000,
            "ad82bf6a9043ceed055076d0fd39f5f186ff8062",
            $"{HuggingFaceBase}/ggml-large-v3.bin"
        ),
        new(
            "large-v3-turbo",
            "Large v3 Turbo",
            "Быстрый вариант large-v3",
            "1.5 ГБ",
            1_619_000_000,
            "4af2b29d7ec73d781377bfd1758ca957a807e941",
            $"{HuggingFaceBase}/ggml-large-v3-turbo.bin"
        ),
        new(
            "tiny-q5_1",
            "Tiny Q5_1",
            "Tiny, квантованная Q5_1",
            "31 МБ",
            32_505_856,
            "2827a03e495b1ed3048ef28a6a4620537db4ee51",
            $"{HuggingFaceBase}/ggml-tiny-q5_1.bin"
        ),
        new(
            "base-q5_1",
            "Base Q5_1",
            "Base, квантованная Q5_1",
            "57 МБ",
            59_768_832,
            "a3733eda680ef76256db5fc5dd9de8629e62c5e7",
            $"{HuggingFaceBase}/ggml-base-q5_1.bin"
        ),
        new(
            "small-q5_1",
            "Small Q5_1",
            "Small, квантованная Q5_1",
            "181 МБ",
            189_792_256,
            "6fe57ddcfdd1c6b07cdcc73aaf620810ce5fc771",
            $"{HuggingFaceBase}/ggml-small-q5_1.bin"
        ),
        new(
            "medium-q5_0",
            "Medium Q5_0",
            "Medium, квантованная Q5_0",
            "514 МБ",
            538_967_040,
            "7718d4c1ec62ca96998f058114db98236937490e",
            $"{HuggingFaceBase}/ggml-medium-q5_0.bin"
        ),
        new(
            "large-v2-q5_0",
            "Large v2 Q5_0",
            "Large v2, квантованная Q5_0",
            "1.1 ГБ",
            1_181_116_006,
            "00e39f2196344e901b3a2bd5814807a769bd1630",
            $"{HuggingFaceBase}/ggml-large-v2-q5_0.bin"
        ),
        new(
            "large-v3-q5_0",
            "Large v3 Q5_0",
            "Large v3, квантованная Q5_0",
            "1.1 ГБ",
            1_181_116_006,
            "e6e2ed78495d403bef4b7cff42ef4aaadcfea8de",
            $"{HuggingFaceBase}/ggml-large-v3-q5_0.bin"
        ),
        new(
            "large-v3-turbo-q5_0",
            "Large v3 Turbo Q5_0",
            "Large v3 Turbo, квантованная Q5_0",
            "547 МБ",
            573_822_976,
            "e050f7970618a659205450ad97eb95a18d69c9ee",
            $"{HuggingFaceBase}/ggml-large-v3-turbo-q5_0.bin"
        ),
        new(
            "tiny-q8_0",
            "Tiny Q8_0",
            "Tiny, квантованная Q8_0",
            "42 МБ",
            44_040_192,
            "19e8118f6652a650569f5a949d962154e01571d9",
            $"{HuggingFaceBase}/ggml-tiny-q8_0.bin"
        ),
        new(
            "base-q8_0",
            "Base Q8_0",
            "Base, квантованная Q8_0",
            "78 МБ",
            81_788_928,
            "7bb89bb49ed6955013b166f1b6a6c04584a20fbe",
            $"{HuggingFaceBase}/ggml-base-q8_0.bin"
        ),
        new(
            "small-q8_0",
            "Small Q8_0",
            "Small, квантованная Q8_0",
            "252 МБ",
            264_241_152,
            "bcad8a2083f4e53d648d586b7dbc0cd673d8afad",
            $"{HuggingFaceBase}/ggml-small-q8_0.bin"
        ),
        new(
            "medium-q8_0",
            "Medium Q8_0",
            "Medium, квантованная Q8_0",
            "785 МБ",
            823_132_160,
            "e66645948aff4bebbec71b3485c576f3d63af5d6",
            $"{HuggingFaceBase}/ggml-medium-q8_0.bin"
        ),
        new(
            "large-v2-q8_0",
            "Large v2 Q8_0",
            "Large v2, квантованная Q8_0",
            "1.5 ГБ",
            1_610_612_736,
            "da97d6ca8f8ffbeeb5fd147f79010eeea194ba38",
            $"{HuggingFaceBase}/ggml-large-v2-q8_0.bin"
        ),
        new(
            "large-v3-turbo-q8_0",
            "Large v3 Turbo Q8_0",
            "Large v3 Turbo, квантованная Q8_0",
            "834 МБ",
            874_362_624,
            "01bf15bedffe9f39d65c1b6ff9b687ea91f59e0e",
            $"{HuggingFaceBase}/ggml-large-v3-turbo-q8_0.bin"
        ),
    ];

    /// <summary>
    /// Возвращает путь, по которому будет сохранена модель.
    /// </summary>
    public string GetModelPath(WhisperModelInfo model)
    {
        var modelsDir = Path.Combine(PathResolver.GetRootDirectory(), "Models");
        return Path.Combine(modelsDir, $"ggml-{model.Name}.bin");
    }

    /// <summary>
    /// Проверяет, скачана ли уже модель.
    /// </summary>
    public bool IsModelDownloaded(WhisperModelInfo model) => File.Exists(GetModelPath(model));

    /// <summary>
    /// Скачивает модель в папку Models с отображением прогресс-бара.
    /// Возвращает путь к скачанному файлу.
    /// </summary>
    public async Task<string> DownloadAsync(WhisperModelInfo model, CancellationToken ct = default)
    {
        var modelsDir = Path.Combine(PathResolver.GetRootDirectory(), "Models");
        Directory.CreateDirectory(modelsDir);

        var destPath = GetModelPath(model);
        var tmpPath = destPath + ".tmp";

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromHours(2);

            using var response = await client.GetAsync(
                model.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                ct
            );
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? model.SizeBytes;

            await AnsiConsole
                .Progress()
                .AutoRefresh(true)
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new TransferSpeedColumn(),
                    new RemainingTimeColumn()
                )
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask(
                        $"[cyan]Загрузка {model.DisplayName}[/]",
                        maxValue: totalBytes
                    );

                    await using var src = await response.Content.ReadAsStreamAsync(ct);
                    await using var dest = File.Create(tmpPath);

                    var buffer = new byte[81920];
                    int read;
                    while ((read = await src.ReadAsync(buffer, ct)) > 0)
                    {
                        await dest.WriteAsync(buffer.AsMemory(0, read), ct);
                        task.Increment(read);
                    }
                });

            AnsiConsole.MarkupLine("[grey]Проверка целостности файла...[/]");
            VerifyHash(tmpPath, model.Sha1Hash);

            File.Move(tmpPath, destPath, overwrite: true);
            return destPath;
        }
        catch
        {
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);
            throw;
        }
    }

    private static void VerifyHash(string filePath, string expectedSha1)
    {
        using var sha1 = SHA1.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha1.ComputeHash(stream);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();

        if (!actual.Equals(expectedSha1, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"SHA-1 хэш не совпадает.\n  Ожидался: {expectedSha1}\n  Получен:  {actual}"
            );
    }
}
