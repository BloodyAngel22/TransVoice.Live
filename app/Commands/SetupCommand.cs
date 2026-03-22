using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using TransVoice.Live.Infrastructure;

namespace TransVoice.Live.Commands;

/// <summary>
/// Команда первоначальной настройки: проверяет системные зависимости и
/// предлагает скачать модель Whisper.
/// </summary>
public class SetupCommand : AsyncCommand<SetupCommand.Settings>
{
    private readonly DependencyChecker _dependencyChecker;
    private readonly ModelDownloader _modelDownloader;

    public class Settings : CommandSettings { }

    public SetupCommand(DependencyChecker dependencyChecker, ModelDownloader modelDownloader)
    {
        _dependencyChecker = dependencyChecker;
        _modelDownloader = modelDownloader;
    }

    /// <summary>
    /// Определяет тип модели по её имени.
    /// </summary>
    private static string GetModelType(string modelName)
    {
        if (modelName.EndsWith("-q5_1"))
            return "Q5_1";
        if (modelName.EndsWith("-q5_0"))
            return "Q5_0";
        if (modelName.EndsWith("-q8_0"))
            return "Q8_0";
        return "Основная";
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken
    )
    {
        AnsiConsole.Write(new FigletText("Setup").Color(Color.Cyan1));
        AnsiConsole.MarkupLine("[grey]TransVoice.Live — мастер первоначальной настройки[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold]Проверка системных зависимостей...[/]");
        var deps = _dependencyChecker.CheckAll();

        bool allOk = true;
        foreach (var dep in deps)
        {
            if (dep.IsAvailable)
            {
                AnsiConsole.MarkupLine($"  [green]✔[/] {dep.Name}");
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]✗[/] {dep.Name} — [red]не найден[/]");
                AnsiConsole.MarkupLine($"    [yellow]{dep.InstallHint}[/]");
                allOk = false;
            }
        }

        AnsiConsole.WriteLine();

        if (!allOk)
        {
            AnsiConsole.MarkupLine(
                "[red]Некоторые зависимости отсутствуют. Установите их и повторите запуск setup.[/]"
            );
            return 1;
        }

        AnsiConsole.MarkupLine("[green]Все зависимости установлены.[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold]Установка модели Whisper[/]");
        AnsiConsole.MarkupLine(
            "[grey]Модели в формате GGML (whisper.cpp). Источник: Hugging Face[/]"
        );
        AnsiConsole.WriteLine();

        var models = ModelDownloader.AvailableModels;

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Модель[/]")
            .AddColumn("[bold]Тип[/]")
            .AddColumn("[bold]Размер[/]")
            .AddColumn("[bold]Описание[/]")
            .AddColumn("[bold]Статус[/]");

        foreach (var m in models)
        {
            var status = _modelDownloader.IsModelDownloaded(m)
                ? "[green]✔ Установлена[/]"
                : "[grey]Не установлена[/]";
            var modelType = GetModelType(m.Name);
            var typeColor = modelType switch
            {
                "Q5_1" => "yellow",
                "Q5_0" => "yellow",
                "Q8_0" => "cyan",
                _ => "green",
            };
            table.AddRow(
                m.DisplayName,
                $"[{typeColor}]{modelType}[/]",
                m.SizeHuman,
                m.Description,
                status
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var choices = models
            .Select(m =>
            {
                var installed = _modelDownloader.IsModelDownloaded(m)
                    ? " [green](установлена)[/]"
                    : "";
                return $"{m.DisplayName} ({m.SizeHuman}){installed}";
            })
            .Append("[grey]Отмена[/]")
            .ToList();

        var selectedLabel = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Выберите [cyan]модель для установки[/]:")
                .PageSize(12)
                .AddChoices(choices)
        );

        if (selectedLabel == "[grey]Отмена[/]")
        {
            AnsiConsole.MarkupLine("[yellow]Установка отменена.[/]");
            return 0;
        }

        var selectedIndex = choices.IndexOf(selectedLabel);
        var selectedModel = models[selectedIndex];

        if (_modelDownloader.IsModelDownloaded(selectedModel))
        {
            var overwrite = AnsiConsole.Prompt(
                new ConfirmationPrompt(
                    $"Модель [cyan]{selectedModel.DisplayName}[/] уже установлена. Перезаписать?"
                )
                {
                    DefaultValue = false,
                }
            );

            if (!overwrite)
            {
                var path = _modelDownloader.GetModelPath(selectedModel);
                AnsiConsole.MarkupLine($"[yellow]Установка пропущена. Путь к модели:[/] {path}");
                return 0;
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            $"[bold]Загрузка [cyan]{selectedModel.DisplayName}[/] ({selectedModel.SizeHuman})...[/]"
        );
        AnsiConsole.MarkupLine($"[grey]URL: {selectedModel.DownloadUrl}[/]");
        AnsiConsole.WriteLine();

        try
        {
            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var destPath = await _modelDownloader.DownloadAsync(selectedModel, cts.Token);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]✔ Модель успешно установлена![/]");
            AnsiConsole.MarkupLine($"[grey]Путь:[/] {destPath}");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(
                "[yellow]Чтобы использовать модель, откройте[/] [bold]settings[/] [yellow]и выберите её.[/]"
            );
            AnsiConsole.MarkupLine("  [grey]transvoice settings[/]");
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Загрузка отменена пользователем.[/]");
            return 2;
        }
        catch (InvalidDataException ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[red]Ошибка проверки целостности:[/] {ex.Message}");
            return 3;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[red]Ошибка загрузки:[/] {ex.Message}");
            return 4;
        }

        return 0;
    }
}
