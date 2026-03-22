using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using TransVoice.Live.Commands;
using TransVoice.Live.Core;
using TransVoice.Live.Infrastructure;
using TransVoice.Live.TextProcessing;

namespace TransVoice.Live;

/// <summary>
/// Главный класс приложения. Настраивает DI-контейнер и запускает CLI-команды.
/// </summary>
public class Program
{
    public static int Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddSingleton<SettingsManager>();
        services.AddSingleton(sp => sp.GetRequiredService<SettingsManager>().Load());
        services.AddSingleton<AudioProcessor>();
        services.AddSingleton<WhisperEngine>();
        services.AddSingleton<AudioStreamer>();
        services.AddSingleton<ClipboardManager>();
        services.AddSingleton<ITextProcessor, RussianTextProcessor>();

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("transvoice");

            config
                .AddCommand<SettingsCommand>("settings")
                .WithDescription("Настройка модели, языка и потоков");

            config
                .AddCommand<LiveCommand>("live")
                .WithDescription("Запуск потокового распознавания");

            config.AddCommand<LiveCommand>("");
        });

        return app.Run(args);
    }
}
