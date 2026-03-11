using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Spectre.Console;
using Spectre.Console.Cli;
using TransVoice.Live.Common;
using TransVoice.Live.Core;
using TransVoice.Live.Infrastructure;

namespace TransVoice.Live.Commands;

public class LiveCommand : AsyncCommand<LiveCommand.Settings>
{
    private readonly AppSettings _appSettings;
    private readonly WhisperEngine _whisperEngine;
    private readonly AudioProcessor _audioProcessor;
    private readonly AudioStreamer _audioStreamer;
    private readonly ClipboardManager _clipboardManager;

    public class Settings : CommandSettings { }

    public LiveCommand(
        AppSettings appSettings,
        WhisperEngine whisperEngine,
        AudioProcessor audioProcessor,
        AudioStreamer audioStreamer,
        ClipboardManager clipboardManager
    )
    {
        _appSettings = appSettings;
        _whisperEngine = whisperEngine;
        _audioProcessor = audioProcessor;
        _audioStreamer = audioStreamer;
        _clipboardManager = clipboardManager;
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken
    )
    {
        if (!_appSettings.IsConfigured)
        {
            AnsiConsole.MarkupLine("[red]Ошибка: Приложение не настроено.[/]");
            AnsiConsole.MarkupLine("Пожалуйста, сначала запустите команду [yellow]settings[/].");
            return 1;
        }

        const int SampleRate = 16000;
        const int BitRate = 16;
        const int BytesPerSample = BitRate / 8;
        const int ChunkDurationMs = 100;
        const int ChunkSize = (SampleRate * ChunkDurationMs) / 1000;
        const int ChunkBytes = ChunkSize * BytesPerSample;
        const int SilenceTimeoutMs = 600;
        const int MaxPhraseDurationMs = 7000;

        var finalChannel = Channel.CreateUnbounded<float[]>();
        var previewChannel = Channel.CreateBounded<float[]>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest }
        );

        StringBuilder fullHistory = new StringBuilder();
        bool thresholdLocked = false;
        bool isProcessingFinal = false;
        CancellationTokenSource? previewCts = null;
        SemaphoreSlim whisperSemaphore = new SemaphoreSlim(1, 1);

        using var rawPipe = _audioStreamer.StartStreaming();

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var samples in finalChannel.Reader.ReadAllAsync())
                {
                    await whisperSemaphore.WaitAsync();
                    try
                    {
                        isProcessingFinal = true;
                        previewCts?.Cancel();
                        _audioProcessor.Normalize(samples);

                        float audioDuration = samples.Length / (float)SampleRate;
                        string prompt = "";
                        lock (fullHistory)
                        {
                            if (fullHistory.Length > 0)
                            {
                                int start = Math.Max(0, fullHistory.Length - 200);
                                prompt = fullHistory.ToString(start, fullHistory.Length - start);
                                prompt = Regex.Replace(prompt, @"\d{2}:\d{2}:\d{2}:", "");
                            }
                        }

                        using var processor = _whisperEngine
                            .GetProcessorBuilder()
                            .WithPrompt(prompt)
                            .WithNoContext()
                            .Build();

                        var swWork = Stopwatch.StartNew();
                        var resultText = new StringBuilder();
                        await foreach (var segment in processor.ProcessAsync(samples))
                        {
                            resultText.Append(segment.Text);
                        }
                        swWork.Stop();

                        var finalLine = resultText.ToString().Trim();
                        if (
                            !string.IsNullOrWhiteSpace(finalLine)
                            && finalLine.Length > 3
                            && !finalLine.StartsWith("[")
                        )
                        {
                            if (!thresholdLocked)
                                thresholdLocked = true;
                            AnsiConsole.Console.Write("\r\x1b[K");
                            AnsiConsole.MarkupLine(
                                $"[grey]{DateTime.Now:HH:mm:ss}[/] [white]{Markup.Escape(finalLine)}[/] [cyan](Work: {swWork.ElapsedMilliseconds}ms, Audio: {audioDuration:F1}s)[/]"
                            );
                            lock (fullHistory)
                            {
                                fullHistory.AppendLine($"{DateTime.Now:HH:mm:ss}: {finalLine}");
                            }
                        }
                    }
                    finally
                    {
                        isProcessingFinal = false;
                        whisperSemaphore.Release();
                    }
                }
            }
            catch { }
        });

        _ = Task.Run(async () =>
        {
            await foreach (var samples in previewChannel.Reader.ReadAllAsync())
            {
                if (isProcessingFinal || finalChannel.Reader.Count > 0)
                    continue;
                await Task.Delay(300);
                if (isProcessingFinal || finalChannel.Reader.Count > 0)
                    continue;

                previewCts = new CancellationTokenSource();
                if (!await whisperSemaphore.WaitAsync(0))
                    continue;

                try
                {
                    _audioProcessor.Normalize(samples);
                    using var processor = _whisperEngine
                        .GetProcessorBuilder()
                        .WithNoContext()
                        .Build();
                    var sw = Stopwatch.StartNew();
                    var resultText = new StringBuilder();
                    await foreach (var segment in processor.ProcessAsync(samples, previewCts.Token))
                    {
                        resultText.Append(segment.Text);
                    }
                    var preview = resultText.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(preview))
                    {
                        AnsiConsole.Console.Write("\r\x1b[K");
                        AnsiConsole.Markup(
                            $"[grey]> {Markup.Escape(preview)}... ({sw.ElapsedMilliseconds}ms)[/]"
                        );
                    }
                }
                catch { }
                finally
                {
                    whisperSemaphore.Release();
                }
            }
        });

        var audioBuffer = new List<float>();
        var preRollBuffer = new Queue<float[]>(8);
        var byteBuffer = new byte[ChunkBytes];
        int silenceSamplesCount = 0;
        DateTime lastPreviewTime = DateTime.MinValue;

        Console.Clear();
        AnsiConsole.Write(new FigletText("TransVoice.Live").Color(Color.Cyan));
        AnsiConsole.MarkupLine("[yellow]> Режим диктовки активен.[/]");
        AnsiConsole.MarkupLine("Калибровка... [grey](Пожалуйста, помолчите 2 сек)[/]");
        AnsiConsole.MarkupLine(
            "[yellow]Нажмите ENTER для завершения записи и копирования в буфер.[/]"
        );

        var calibrationLevels = new List<float>();
        for (int i = 0; i < 20; i++)
        {
            int bytesRead = 0;
            while (bytesRead < ChunkBytes)
            {
                int r = await rawPipe.ReadAsync(byteBuffer, bytesRead, ChunkBytes - bytesRead);
                if (r <= 0)
                    break;
                bytesRead += r;
            }
            var chunk = new float[ChunkSize];
            var (rawRms, _) = _audioProcessor.ProcessChunk(byteBuffer, chunk, ChunkSize, false);
            calibrationLevels.Add(rawRms);
            Console.Write(".");
        }

        float medianBackground = calibrationLevels
            .OrderBy(x => x)
            .ElementAt(calibrationLevels.Count / 2);

        float targetRMS = 0.12f;
        float maxGain = 12.0f;
        _audioProcessor.SetupAGC(medianBackground, targetRMS, maxGain);

        float startThr = medianBackground * 2.5f + 0.004f;
        float stopThr = medianBackground * 1.5f + 0.002f;
        Console.WriteLine($"\nКалибровка завершена. (Шум: {medianBackground:F5})");

        while (true)
        {
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter)
                break;

            int bytesRead = 0;
            while (bytesRead < ChunkBytes)
            {
                int r = await rawPipe.ReadAsync(byteBuffer, bytesRead, ChunkBytes - bytesRead);
                if (r <= 0)
                    break;
                bytesRead += r;
            }

            var currentChunk = new float[ChunkSize];
            var (rawRms, processedRms) = _audioProcessor.ProcessChunk(
                byteBuffer,
                currentChunk,
                ChunkSize,
                isSpeaking
            );

            if (!isSpeaking)
            {
                if (!thresholdLocked && rawRms < startThr * 0.9f)
                {
                    medianBackground = medianBackground * 0.99f + rawRms * 0.01f;
                    startThr = medianBackground * 2.5f + 0.004f;
                    stopThr = medianBackground * 1.5f + 0.002f;
                }
                if (rawRms > startThr)
                {
                    isSpeaking = true;
                    silenceSamplesCount = 0;
                    while (preRollBuffer.Count > 0)
                        audioBuffer.AddRange(preRollBuffer.Dequeue());
                    audioBuffer.AddRange(currentChunk);
                }
                else
                {
                    preRollBuffer.Enqueue(currentChunk);
                    if (preRollBuffer.Count > 8)
                        preRollBuffer.Dequeue();
                }
            }
            else
            {
                audioBuffer.AddRange(currentChunk);
                if (
                    audioBuffer.Count > SampleRate * 2.5
                    && (DateTime.Now - lastPreviewTime).TotalMilliseconds > 3000
                )
                {
                    previewChannel.Writer.TryWrite(audioBuffer.ToArray());
                    lastPreviewTime = DateTime.Now;
                }

                if (rawRms < stopThr)
                    silenceSamplesCount += ChunkDurationMs;
                else
                    silenceSamplesCount = 0;

                if (
                    silenceSamplesCount >= SilenceTimeoutMs
                    || audioBuffer.Count >= (SampleRate * MaxPhraseDurationMs / 1000)
                )
                {
                    if (audioBuffer.Count > SampleRate * 0.4)
                        finalChannel.Writer.TryWrite(audioBuffer.ToArray());
                    audioBuffer.Clear();
                    isSpeaking = false;
                    silenceSamplesCount = 0;
                    preRollBuffer.Clear();
                    lastPreviewTime = DateTime.MinValue;
                }
            }

            string lockStatus = thresholdLocked ? "[blue]LOCKED[/]" : "[yellow]ADAPT [/]";

            float displayRms = isSpeaking ? processedRms : rawRms;
            int filled = (int)Math.Min(20, (displayRms / (startThr * 1.5f)) * 20);
            string bar = new string('■', filled).PadRight(20, '·');
            AnsiConsole.Console.Write("\r\x1b[K");
            AnsiConsole.Markup(
                $"{lockStatus} | Mic: [green]{bar}[/] ({rawRms:F4}) | {(isSpeaking ? "[red]● REC[/]" : "[grey]IDLE [/]")}"
            );
        }
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("ПОЛНЫЙ ТЕКСТ").Centered().RuleStyle("cyan"));
        var fullText = fullHistory.ToString();
        Console.WriteLine(fullText);
        _clipboardManager.CopyText(fullText);
        AnsiConsole.MarkupLine("\n[green]✔ Текст скопирован в буфер обмена![/]");

        return 0;
    }

    private bool isSpeaking = false;
}
