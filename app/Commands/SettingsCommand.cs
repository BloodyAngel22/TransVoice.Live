using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using TransVoice.Live.Common;
using TransVoice.Live.Infrastructure;

namespace TransVoice.Live.Commands;

/// <summary>
/// Команда интерактивной настройки приложения: выбор модели, языка и количества потоков.
/// </summary>
public class SettingsCommand : Command<SettingsCommand.Settings>
{
    private readonly SettingsManager _settingsManager;

    public class Settings : CommandSettings { }

    public SettingsCommand(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
    }

    public override int Execute(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken
    )
    {
        AnsiConsole.Write(new FigletText("Settings").Color(Color.Yellow));

        var modelsDir = Path.Combine(PathResolver.GetRootDirectory(), "Models");
        if (!Directory.Exists(modelsDir))
            Directory.CreateDirectory(modelsDir);

        var modelFiles = Directory.GetFiles(modelsDir, "*.bin");

        if (modelFiles.Length == 0)
        {
            AnsiConsole.MarkupLine($"[red]Файлы моделей не найдены в директории {modelsDir}.[/]");
            AnsiConsole.MarkupLine(
                "[yellow]Пожалуйста, добавьте .bin модели Whisper в папку Models и запустите settings снова.[/]"
            );
            return 1;
        }

        var currentSettings = _settingsManager.Load();
        bool isConfigured = currentSettings.IsConfigured;

        var modelChoices = new List<string>();
        if (isConfigured)
            modelChoices.Add("Пропустить");

        foreach (var file in modelFiles)
        {
            var fileName = Path.GetFileName(file);
            if (isConfigured && file == currentSettings.ModelPath)
                modelChoices.Add($"[green]✔[/] {fileName}");
            else
                modelChoices.Add(fileName);
        }

        var selectedModelChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Выберите [green]модель Whisper[/]:")
                .PageSize(10)
                .AddChoices(modelChoices)
        );

        if (selectedModelChoice != "Пропустить")
        {
            var pureFileName = selectedModelChoice.Replace("[green]✔[/] ", "");
            currentSettings.ModelPath = Path.Combine(modelsDir, pureFileName);
        }

        var langChoices = new List<string>();
        if (isConfigured)
            langChoices.Add("Пропустить");

        foreach (var lang in new[] { "auto", "ru", "en" })
        {
            if (isConfigured && lang == currentSettings.Language)
                langChoices.Add($"[green]✔[/] {lang}");
            else
                langChoices.Add(lang);
        }

        var selectedLangChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Выберите [green]язык распознавания[/]:")
                .AddChoices(langChoices)
        );

        if (selectedLangChoice != "Пропустить")
        {
            currentSettings.Language = selectedLangChoice.Replace("[green]✔[/] ", "");
        }

        if (isConfigured)
        {
            var threadAction = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Настройка [green]потоков[/]:")
                    .AddChoices(
                        new[]
                        {
                            $"[green]✔[/] Пропустить ({currentSettings.Threads})",
                            "Ввести новое значение",
                        }
                    )
            );

            if (threadAction == "Ввести новое значение")
            {
                currentSettings.Threads = AnsiConsole.Prompt(
                    new TextPrompt<int>("Введите [green]количество потоков[/] (threads):")
                        .DefaultValue(currentSettings.Threads)
                        .Validate(t =>
                            t > 0 && t <= Environment.ProcessorCount
                                ? ValidationResult.Success()
                                : ValidationResult.Error(
                                    "[red]Число потоков должно быть от 1 до "
                                        + Environment.ProcessorCount
                                        + "[/]"
                                )
                        )
                );
            }
        }
        else
        {
            currentSettings.Threads = AnsiConsole.Prompt(
                new TextPrompt<int>("Введите [green]количество потоков[/] (threads):")
                    .DefaultValue(Environment.ProcessorCount / 2)
                    .Validate(t =>
                        t > 0 && t <= Environment.ProcessorCount
                            ? ValidationResult.Success()
                            : ValidationResult.Error(
                                "[red]Число потоков должно быть от 1 до "
                                    + Environment.ProcessorCount
                                    + "[/]"
                            )
                    )
            );
        }

        _settingsManager.Save(currentSettings);

        AnsiConsole.MarkupLine("[green]✔ Настройки успешно сохранены![/]");
        return 0;
    }
}
